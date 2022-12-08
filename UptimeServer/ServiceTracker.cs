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
        public WebService(Services service) : this(service.Name, service.Address, service.DisplayAddress, service.External, service.Backend, "*UNTESTED*", service.TrustCertificate, service.CheckType, DateTime.MaxValue) { }
    }
    public static class ServiceTracker
    {
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
        private static bool locked = false;
        private static bool resetServices = true;
        private static WebService[] WebServices = new WebService[0];
        private static Task ServiceTrackingTask = CheckServices();
        private static async Task CheckServices()
        {
            bool havelock;
            lock (lockobject)
            {
                if (!locked)
                {
                    locked = true;
                    havelock = true;
                }
                else
                {
                    havelock = false;
                }
            }
            if (!havelock) { return; }
            try
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
                        ParallelOptions po = new ParallelOptions() { MaxDegreeOfParallelism = 5 };
                        Parallel.For(0, WebServices.Length, po, (i,x) =>
                        {
                            CheckResults[i] = WebServices[i].checktype switch
                            {
                                CheckType.PING => CheckPING(WebServices[i], Now),
                                CheckType.TCP => CheckTCP(WebServices[i], Now),
                                CheckType.SSL => CheckSSL(WebServices[i], Now),
                                CheckType.HTTP => CheckHTTP(WebServices[i], Now),
                                _ => throw new NotImplementedException(),
                            };
                        });
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
                                    await MattermostLogger.DefaultLogger.SendMessage($"Service {webService.name} {(state ? "is now up!" : "has gone down! @ahendrix")}");
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
                    Now = DateTime.UtcNow;
                    await Task.Delay(30000 - (((Now.Second * 1000) + Now.Millisecond) % 30000));
                }
            }
            finally
            {
                lock (lockobject)
                {
                    locked = false;
                }
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
                    bool ok = response.StatusCode == System.Net.HttpStatusCode.OK;
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
                    return service with { live = "Error:Invalid address", checktime = DateTime.MaxValue };
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
                            return service with { live = "OK", checktime = prev ? Now : service.checktime };
                        }
                        if (TCPConnection.IsFaulted)
                        {
                            Console.Error.WriteLine("Error for Service " + service.name);
                            Console.Error.WriteLine(TCPConnection.Exception?.ToString());
                            return service with { live = "Error:Exception!", checktime = DateTime.MaxValue };
                        }
                        onesecond.Cancel();
                    }
                }
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
