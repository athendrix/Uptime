using CSL.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UptimeServer.Data;

using static UptimeServer.AsyncLocker;

namespace UptimeServer
{
    public record WebService(string name, string address, string? displayaddress, bool external, string? backend, string live, bool trustcert, CheckType checktype, DateTime checktime, string? errorText)
    {
        public WebService(Services service) : this(service.Name, service.Address, service.DisplayAddress, service.External, service.Backend, "*UNTESTED*", service.TrustCertificate, service.CheckType, DateTime.MaxValue, null) { }
    }
    public static class ServiceTracker
    {
        private static FastHTTPChecker httpChecker = new FastHTTPChecker(2000);
        public static bool IsRunning => ServiceTrackingTask.Status == TaskStatus.Running;
        public static void Start()
        {
            if (!IsRunning)
            {
                Task TestStart = CheckServices();
                if (TestStart.Status == TaskStatus.Running)
                {
                    ServiceTrackingTask = TestStart;
                }
            }
        }
        public static void Reset() => resetServices = true;
        public static ReadOnlyMemory<WebService> GetServices() => WebServices.AsMemory();

        private static object lockobject = new object();
        private static bool resetServices = true;
        
        private static WebService[] WebServices = new WebService[0];
        private static Task ServiceTrackingTask = CheckServices();
        private static async Task CheckServices()
        {
            await TryLock(lockobject, async () =>
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
                        Task<WebService>[] CheckResults = new Task<WebService>[WebServices.Length];
                        for (int i = 0; i < WebServices.Length; i++)
                        {
                            CheckResults[i] = Check(WebServices[i], Now);
                        }
                        await Task.WhenAll(CheckResults);
                        for (int i = 0; i < WebServices.Length; i++)
                        {
                            WebService webService = WebServices[i];
                            WebService result = CheckResults[i].Result;
                            bool oldstate = !(webService.checktime == DateTime.MaxValue);
                            bool state = !(result.checktime == DateTime.MaxValue);

                            if (MattermostLogger.DefaultLogger != null &&
                                webService.name == result.name &&
                                !webService.live.Contains("UNTESTED") &&
                                !result.live.Contains("UNTESTED") &&
                                state != oldstate)
                            {
                                try
                                {
                                    await MattermostLogger.DefaultLogger.SendMessage($"Service {webService.name} {(state ? "is now up!" : "has gone down! @ahendrix")}", result.live + "\n\n---\n\n" + result.errorText);
                                }
                                catch { }
                            }
                            WebServices[i] = result;
                        }
                        //WebServices = CheckResults.Select(x => x.Result).ToArray();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                    Console.WriteLine($"Elapsed Time for Cycle: {(DateTime.UtcNow - Now).TotalSeconds} seconds.");
                    Now = DateTime.UtcNow;
                    await Task.Delay(30000 - (((Now.Second * 1000) + Now.Millisecond) % 30000));
                }
            });
        }
#warning Simplify Error Reporting
        private static Task<WebService> Check(WebService service, DateTime Now) => service.checktype switch
        {
            CheckType.PING => CheckPING(service, Now),
            CheckType.TCP => CheckTCP(service, Now),
            CheckType.SSL => CheckSSL(service, Now),
            CheckType.HTTP => CheckHTTP(service, Now),
            _ => throw new NotImplementedException(),
        };
        private static async Task<WebService> CheckHTTP(WebService service, DateTime Now)
        {
            WebService? serverError = null;
            WebService clientError = service;
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    int response = await httpChecker.CheckAsync(service.address, service.trustcert);
                    switch (response)
                    {
                        case -1:
                            clientError = service with { live = "Error:TLS/Certificate", checktime = DateTime.MaxValue, errorText = null };
                            continue;
                        case -2:
                            clientError = service with { live = "Error:Connection Refused", checktime = DateTime.MaxValue, errorText = null };
                            continue;
                        case -3:
                            clientError = service with { live = "Error:DNS", checktime = DateTime.MaxValue, errorText = null };
                            continue;
                        case -4:
                            clientError = service with { live = "Error:No Response", checktime = DateTime.MaxValue, errorText = null };
                            continue;
                    }
                    HttpStatusCode StatusCode = (HttpStatusCode)response;
                    bool ok = StatusCode == HttpStatusCode.OK;
                    bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                    if (ok)
                    {
                        return service with { live = $"{StatusCode}:{response}", checktime = prev ? Now : service.checktime, errorText = null };
                    }
                    serverError = service with { live = $"Error:{StatusCode}:{response}", checktime = DateTime.MaxValue, errorText = null};
                }
                catch (HttpRequestException hre)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Unrecognized error for Service " + service.name);
                    sb.AppendLine(hre.Message);
                    sb.AppendLine("Status Code: " + (int)hre.StatusCode.GetValueOrDefault());
                    sb.AppendLine("---");
                    sb.AppendLine(hre.ToString());
                    clientError = service with { live = "Error:Exception!", checktime = DateTime.MaxValue, errorText = sb.ToString() };
                    continue;
                }
                catch (Exception e)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Error for Service " + service.name);
                    sb.AppendLine(e.ToString());
                    clientError = service with { live = "Error:Exception!", checktime = DateTime.MaxValue, errorText = sb.ToString() };
                    continue;
                }
            }
            return serverError ?? clientError;

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
                    return service with { live = "Error:Exception!", checktime = DateTime.MaxValue, errorText = e.ToString() };
                }
            }
        }
        private static Task<WebService> CheckSSL(WebService service, DateTime Now)
        {
            return Task.FromResult(service with { live = "Error:Check Not Implemented!", checktime = DateTime.MaxValue });
        }
        private static async Task<WebService> CheckTCP(WebService service, DateTime Now)
        {

            try
            {
                string[] hostport = service.address.Split(":");
                int port;
                if (hostport.Length != 2 || !int.TryParse(hostport[1], out port))
                {
                    return service with { live = "Error:Invalid address", checktime = DateTime.MaxValue, errorText = null };
                }
                for (int i = 0; i < 4; i++)
                {
                    CancellationTokenSource onesecond = new CancellationTokenSource();
                    using (TcpClient client = new TcpClient())
                    {
                        Task TCPConnection = client.ConnectAsync(hostport[0], port, onesecond.Token).AsTask();
                        await Task.WhenAny(Task.Delay(1000), TCPConnection);
                        if (TCPConnection.IsCompletedSuccessfully)
                        {
                            bool prev = service.live.Contains("Error:") || service.live.Contains("UNTESTED");
                            return service with { live = "OK", checktime = prev ? Now : service.checktime, errorText = null };
                        }
                        if (TCPConnection.IsFaulted)
                        {
                            return service with { live = "Error:Exception!", checktime = DateTime.MaxValue, errorText = TCPConnection.Exception?.ToString() };
                        }
                        onesecond.Cancel();
                    }
                }
                return service with { live = "Error:Timeout", checktime = DateTime.MaxValue, errorText = null };
            }
            catch (Exception e)
            {
                return service with { live = "Error:Exception!", checktime = DateTime.MaxValue, errorText = e.Message };
            }

        }
    }
}
