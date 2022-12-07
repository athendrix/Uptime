using CSL.SQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using UptimeServer.Data;

namespace UptimeServer.Controllers
{
    public record WebService(string name, string address, string? displayaddress, bool external, string? backend, string live, bool trustcert, CheckType checktype, DateTime checktime);
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceListController : ControllerBase
    {
        static DateTime Now = DateTime.UtcNow;
        static ServiceListController()
        {
            Task.Run(async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                while (true)
                {
                    stopwatch.Restart();
                    Now = DateTime.UtcNow;
                    if (resetServices)
                    {
                        try
                        {
                            List<WebService> NewList = new List<WebService>();
                            using (SQLDB sql = await PostgresServer.GetSQL())
                            {
                                await Services.CreateDB(sql);
                                foreach (Services row in await Services.Select(sql))
                                {
                                    NewList.Add(new WebService(row.Name, row.Address, row.DisplayAddress, row.External, row.Backend, "*UNTESTED*", row.TrustCertificate, row.CheckType, DateTime.MaxValue));
                                }
                            }
                            WebServices = NewList.ToArray();
                            resetServices = false;
                        }
                        catch(Exception e)
                        {
                            Console.Error.WriteLine(e.ToString());
                        }
                    }
                    try
                    {
                        ParallelOptions options = new ParallelOptions();
                        options.MaxDegreeOfParallelism = 5;
                        Parallel.For(0, WebServices.Length, options, async (i) =>
                        {
                            WebServices[i] = WebServices[i].checktype switch
                            {
                                CheckType.PING => await CheckPING(WebServices[i]),
                                CheckType.TCP => await CheckTCP(WebServices[i]),
                                CheckType.SSL => await CheckSSL(WebServices[i]),
                                CheckType.HTTP => await CheckHTTP(WebServices[i]),
                                _ => throw new NotImplementedException(),
                            };
                        });
                    }
                    catch(Exception e)
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                    int timetaken = (int)stopwatch.ElapsedMilliseconds;
                    if(timetaken < 30000)
                    {
                        await Task.Delay(30000 - timetaken);
                    }
                }
            });
        }
        private static WebService[] WebServices = new WebService[0];
        private static bool resetServices = true;

        [HttpGet]
        public async Task<IEnumerable<WebService>> Get()
        {
            WebService[]? toReturn = WebServices?.ToArray();
            while(toReturn == null)
            {
                await Task.Delay(100);
                toReturn = WebServices?.ToArray();
            }
            return toReturn.OrderBy((srv) => srv.checktime).ThenBy((srv) => srv.name);
            
        }

        [HttpPost]
        public async Task<IActionResult> Add(string Name, string Address, bool External, string Backend, string? DisplayAddress = null, bool TrustCertificate = false, CheckType CheckType = CheckType.HTTP)
        {
            using (SQLDB sql = await PostgresServer.GetSQL())
            {
                Services toInsert = new Services(Name, Address, External, Backend, DisplayAddress, TrustCertificate, CheckType);
                int toReturn = await toInsert.Insert(sql);
                if (toReturn > 0)
                {
                    resetServices = true;
                    return Ok();
                }
            }
            return BadRequest();
        }

        [HttpDelete]
        public async Task<IActionResult> Remove(string Name)
        {
            using (SQLDB sql = await PostgresServer.GetSQL())
            {
                int toReturn = await Services.DeleteBy_Name(sql, Name);
                if (toReturn > 0)
                {
                    resetServices = true;
                    return Ok();
                }
            }
            return BadRequest();
        }

        [HttpPut]
        public async Task<IActionResult> Put(string Name, string Address, bool External, string Backend, string? DisplayAddress = null, bool TrustCertificate = false, CheckType CheckType = CheckType.HTTP)
        {
            using (SQLDB sql = await PostgresServer.GetSQL())
            {
                Services toInsert = new Services(Name, Address, External, Backend, DisplayAddress, TrustCertificate, CheckType);
                int toReturn = await toInsert.Upsert(sql);
                if (toReturn > 0)
                {
                    resetServices = true;
                    return Ok();
                }
            }
            return BadRequest();
        }
        private static async Task<WebService> CheckHTTP(WebService service)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if(service.trustcert)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Head, service.address);
                    HttpResponseMessage response = await client.SendAsync(message);
                    bool ok = response.StatusCode.ToString() == "OK";
                    bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                    return service with { live = (ok ? "" : "Error:") + response.StatusCode.ToString() + ":" + ((int)response.StatusCode).ToString(), checktime = ok?(prev ? Now : service.checktime):DateTime.MaxValue };
                }
                catch (HttpRequestException hre)
                {
                    if (hre.InnerException is AuthenticationException ae)
                    {
                        return service with { live = "Error:TLS/Certificate", checktime = DateTime.MaxValue };
                    }
                    if (hre.Message.Contains("Connection refused"))
                    {
                        return service with { live = "Error:Connection Refused", checktime = DateTime.MaxValue };
                    }
                    if (hre.Message.Contains("Name does not resolve"))
                    {
                        return service with { live = "Error:DNS", checktime = DateTime.MaxValue };
                    }
                    Console.Error.WriteLine("----------------------------------------------------------");
                    Console.Error.WriteLine("Error for Service " + service.name);
                    Console.Error.WriteLine(hre.Message);
                    Console.Error.WriteLine("Status Code: " + (int)hre.StatusCode.GetValueOrDefault());
                    Console.Error.WriteLine("----------------------------------------------------------");
                    Console.Error.WriteLine(hre.ToString());
                    Console.Error.WriteLine("----------------------------------------------------------");
                    return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Error for Service " + service.name);
                    Console.Error.WriteLine(e.ToString());
                    return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                }
            }
        }
        private static async Task<WebService> CheckPING(WebService service)
        {
            using (Ping client = new Ping())
            {
                try
                {
                    PingReply response = await client.SendPingAsync(service.address, 1000);
                    for (int i = 1; i < 4; i++)
                    {
                        if (response.Status == IPStatus.Success) { break; }
                        response = await client.SendPingAsync(service.address, 1000);
                    }
                    bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                    string status = response.Status == IPStatus.Success ? "OK" : "Error:" + response.Status.ToString();
                    return service with { live = status, checktime = prev ? Now : service.checktime };
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Error for Service " + service.name);
                    Console.Error.WriteLine(e.ToString());
                    return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                }
            }
        }
        private static  Task<WebService> CheckSSL(WebService service)
        {
            throw new NotImplementedException();
        }
        private static async Task<WebService> CheckTCP(WebService service)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    string[] hostport = service.address.Split(":");
                    int port;
                    if (hostport.Length != 2 || !int.TryParse(hostport[1], out port))
                    {
                        return service with { live = "Error:Invalid address", checktime = DateTime.MaxValue };
                    }
                    CancellationTokenSource onesecond = new CancellationTokenSource();
                    Task TCPConnection = client.ConnectAsync(hostport[0], port,onesecond.Token).AsTask();
                    await Task.WhenAny(Task.Delay(1000), TCPConnection);
                    if(TCPConnection.IsCompletedSuccessfully)
                    {
                        bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                        return service with { live = "OK", checktime = prev ? Now : service.checktime };
                    }
                    if(TCPConnection.IsFaulted)
                    {
                        Console.Error.WriteLine("Error for Service " + service.name);
                        Console.Error.WriteLine(TCPConnection.Exception?.ToString());
                        return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                    }
                    onesecond.Cancel();
                    return service with { live = "Error:Timeout", checktime = DateTime.MaxValue };
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Error for Service " + service.name);
                    Console.Error.WriteLine(e.ToString());
                    return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                }
            }
        }
    }
}