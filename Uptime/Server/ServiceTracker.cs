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
// using Uptime.Server.Data;
using Uptime.Shared;
using Uptime.Server.Data;
using Uptime.Shared.Data;
using static Uptime.Server.AsyncLocker;
using CheckType = Uptime.Shared.CheckType;

namespace Uptime.Server
{
    // public record WebService(string name, string address, string? displayaddress, bool external, string? backend, string live, bool trustcert, CheckType checktype, DateTime checktime, string? errorText)
    // {
    //     public WebService(Services service) : this(service.Name, service.Address, service.DisplayAddress, service.External, service.Backend, "*UNTESTED*", service.TrustCertificate, service.CheckType, DateTime.MaxValue, null) { }
    // }
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
        public static List<ServiceRecord> GetServices() => WebServices;

        private static object lockobject = new object();
        private static bool resetServices = true;
        
        private static List<ServiceRecord> WebServices = new();
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
                                    WebServices = DBServices.Select(ServiceRecord.FromService).ToList();
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
                        Task<ServiceRecord>[] checkResults = new Task<ServiceRecord>[WebServices.Count];
                        for (int i = 0; i < WebServices.Count; i++)
                        {
                            checkResults[i] = Check(WebServices.ElementAt(i), Now);
                        }
                        await Task.WhenAll(checkResults);
                        for (int i = 0; i < WebServices.Count; i++)
                        {
                            ServiceRecord webService = WebServices[i];
                            ServiceRecord result = checkResults[i].Result;
                            bool oldstate = !(webService.CheckTime == DateTime.MaxValue);
                            bool state = !(result.CheckTime == DateTime.MaxValue);

                            if (MattermostLogger.DefaultLogger != null &&
                                webService.Name == result.Name &&
                                !webService.Live.Contains("UNTESTED") &&
                                !result.Live.Contains("UNTESTED") &&
                                state != oldstate)
                            {
                                try
                                {
                                    await MattermostLogger.DefaultLogger.SendMessage($"Service {webService.Name} {(state ? "is now up!" : "has gone down! @ahendrix")}", result.Live + "\n\n---\n\n" + result.ErrorText);
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
        private static Task<ServiceRecord> Check(ServiceRecord service, DateTime Now) => service.CheckType switch
        {
            CheckType.PING => CheckPING(service, Now),
            CheckType.TCP => CheckTCP(service, Now),
            CheckType.SSL => CheckSSL(service, Now),
            CheckType.HTTP => CheckHTTP(service, Now),
            _ => throw new NotImplementedException(),
        };
        private static async Task<ServiceRecord> CheckHTTP(ServiceRecord service, DateTime Now)
        {
            ServiceRecord? serverError = null;
            ServiceRecord clientError = service;
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    int response = await httpChecker.CheckAsync(service.Address, service.TrustCert);
                    switch (response)
                    {
                        case -1:
                            clientError = service with { Live = "Error:TLS/Certificate", CheckTime = DateTime.MaxValue, ErrorText = null };
                            continue;
                        case -2:
                            clientError = service with { Live = "Error:Connection Refused", CheckTime = DateTime.MaxValue, ErrorText = null };
                            continue;
                        case -3:
                            clientError = service with { Live = "Error:DNS", CheckTime = DateTime.MaxValue, ErrorText = null };
                            continue;
                        case -4:
                            clientError = service with { Live = "Error:No Response", CheckTime = DateTime.MaxValue, ErrorText = null };
                            continue;
                    }
                    HttpStatusCode statusCode = (HttpStatusCode)response;
                    bool ok = statusCode == HttpStatusCode.OK;
                    bool prev = service.Live.Contains("Error:") || service.Live.Contains("UNTESTED");
                    if (ok)
                    {
                        return service with { Live = $"{statusCode}:{response}", CheckTime = prev ? Now : service.CheckTime, ErrorText = null };
                    }
                    serverError = service with { Live = $"Error:{statusCode}:{response}", CheckTime = DateTime.MaxValue, ErrorText = null};
                }
                catch (HttpRequestException hre)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Unrecognized error for Service " + service.Name);
                    sb.AppendLine(hre.Message);
                    sb.AppendLine("Status Code: " + (int)hre.StatusCode.GetValueOrDefault());
                    sb.AppendLine("---");
                    sb.AppendLine(hre.ToString());
                    clientError = service with { Live = "Error:Exception!", CheckTime = DateTime.MaxValue, ErrorText = sb.ToString() };
                    continue;
                }
                catch (Exception e)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Error for Service " + service.Name);
                    sb.AppendLine(e.ToString());
                    clientError = service with { Live = "Error:Exception!", CheckTime = DateTime.MaxValue, ErrorText = sb.ToString() };
                    continue;
                }
            }
            return serverError ?? clientError;

        }
        private static async Task<ServiceRecord> CheckPING(ServiceRecord service, DateTime Now)
        {
            using (Ping client = new Ping())
            {
                try
                {
                    PingReply response = await client.SendPingAsync(service.Address, 1000);
                    for (int i = 1; i < 4; i++)
                    {
                        if (response.Status == IPStatus.Success) { break; }
                        response = await client.SendPingAsync(service.Address, 1000);
                    }
                    bool prev = service.Live.Contains("Error:") || service.Live.Contains("UNTESTED");
                    string status = response.Status == IPStatus.Success ? "OK" : "Error:" + response.Status.ToString();
                    return service with { Live = status, CheckTime = prev ? Now : service.CheckTime };
                }
                catch (Exception e)
                {
                    return service with { Live = "Error:Exception!", CheckTime = DateTime.MaxValue, ErrorText = e.ToString() };
                }
            }
        }
        private static Task<ServiceRecord> CheckSSL(ServiceRecord service, DateTime Now)
        {
            return Task.FromResult(service with { Live = "Error:Check Not Implemented!", CheckTime = DateTime.MaxValue });
        }
        private static async Task<ServiceRecord> CheckTCP(ServiceRecord service, DateTime Now)
        {

            try
            {
                string[] hostport = service.Address.Split(":");
                int port;
                if (hostport.Length != 2 || !int.TryParse(hostport[1], out port))
                {
                    return service with { Live = "Error:Invalid address", CheckTime = DateTime.MaxValue, ErrorText = null };
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
                            bool prev = service.Live.Contains("Error:") || service.Live.Contains("UNTESTED");
                            return service with { Live = "OK", CheckTime = prev ? Now : service.CheckTime, ErrorText = null };
                        }
                        if (TCPConnection.IsFaulted)
                        {
                            return service with { Live = "Error:Exception!", CheckTime = DateTime.MaxValue, ErrorText = TCPConnection.Exception?.ToString() };
                        }
                        onesecond.Cancel();
                    }
                }
                return service with { Live = "Error:Timeout", CheckTime = DateTime.MaxValue, ErrorText = null };
            }
            catch (Exception e)
            {
                return service with { Live = "Error:Exception!", CheckTime = DateTime.MaxValue, ErrorText = e.Message };
            }

        }
    }
}
