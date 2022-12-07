using CSL.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using UptimeServer.Data;

namespace UptimeServer
{
    public record WebService(string name, string address, string? displayaddress, bool external, string? backend, string live, bool trustcert, CheckType checktype, DateTime checktime)
    {
        public WebService(Services service) : this(service.Name, service.Address, service.DisplayAddress, service.External, service.Backend, "*UNTESTED*", service.TrustCertificate, service.CheckType, DateTime.MaxValue) {}
    }
    public static class ServiceTracker
    {
        public static bool IsRunning => ServiceTrackingTask.Status == TaskStatus.Running;
        public static void Restart() => ServiceTrackingTask = CheckServices();
        public static void Start() { if (!IsRunning) { Restart(); } }
        public static void Reset() => resetServices = true;
        public static ReadOnlyMemory<WebService> GetServices() => WebServices.AsMemory();

        private static Task ServiceTrackingTask = CheckServices();
        private static bool resetServices = true;
        private static WebService[] WebServices = new WebService[0];
        private static async Task CheckServices()
        {
            DateTime Now;
            while (true)
            {
                Now = DateTime.UtcNow;
                if (resetServices)
                {
                    try
                    {
                        using (SQLDB sql = await PostgresServer.GetSQL())
                        {
                            await Services.CreateDB(sql);
                            using (AutoClosingEnumerable<Services> DBServices = await Services.Select(sql))
                            {
                                WebServices = DBServices.Select(x => new WebService(x)).ToArray();
                            }
                        }
                        resetServices = false;
                    }
                    catch (Exception e)
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
                            CheckType.PING => await CheckPING(WebServices[i], Now),
                            CheckType.TCP => await CheckTCP(WebServices[i], Now),
                            CheckType.SSL => await CheckSSL(WebServices[i], Now),
                            CheckType.HTTP => await CheckHTTP(WebServices[i], Now),
                            _ => throw new NotImplementedException(),
                        };
                    });
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
                Now = DateTime.UtcNow;
                await Task.Delay(30000 - (((Now.Second * 1000) + Now.Millisecond) % 30000));
            }

        }
#warning Simplify Error Reporting
        private static async Task<WebService> CheckHTTP(WebService service, DateTime Now)
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (service.trustcert)
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
                    return service with { live = (ok ? "" : "Error:") + response.StatusCode.ToString() + ":" + ((int)response.StatusCode).ToString(), checktime = ok ? (prev ? Now : service.checktime) : DateTime.MaxValue };
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
        private static async Task<WebService> CheckPING(WebService service, DateTime Now)
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
        private static Task<WebService> CheckSSL(WebService service, DateTime Now)
        {
            throw new NotImplementedException();
        }
        private static async Task<WebService> CheckTCP(WebService service, DateTime Now)
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
                    Task TCPConnection = client.ConnectAsync(hostport[0], port, onesecond.Token).AsTask();
                    await Task.WhenAny(Task.Delay(1000), TCPConnection);
                    if (TCPConnection.IsCompletedSuccessfully)
                    {
                        bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                        return service with { live = "OK", checktime = prev ? Now : service.checktime };
                    }
                    if (TCPConnection.IsFaulted)
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
