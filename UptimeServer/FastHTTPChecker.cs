using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace UptimeServer
{
    public class FastHTTPChecker
    {
        private int Timeout;
        public static long msMultiplier = new TimeSpan(0, 0, 0, 0, 1).Ticks;
        private class HttpClientWrapper
        {
            private bool Ready = true;
            private HttpClient client;
            private int Timeout;
            public HttpClientWrapper(bool AcceptAnyCert, int Timeout)
            {
                this.Timeout = Timeout;
                HttpClientHandler handler = new HttpClientHandler();
                if (AcceptAnyCert) { handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator; }
                client = new HttpClient(handler);
                client.Timeout = new TimeSpan(Timeout * msMultiplier);
                Ready = true;
            }
            public async Task<int?> CheckAsyncIfReady(string uri)
            {
                #region Locking
                bool AcquiredLock = false;
                lock (this)
                {
                    if (Ready)
                    {
                        AcquiredLock = true;
                        Ready = false;
                    }
                }
                if (!AcquiredLock) return null;
                #endregion
                try
                {

                    HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Head, uri);
#pragma warning disable CS8619 // I'm making the non-null value look nullable so it'll match the nullable one below.
                    Task<HttpResponseMessage?> SendTask = client.SendAsync(message);
#pragma warning restore CS8619 // Pure silliness, this is totally safe.
                    Task<HttpResponseMessage?> TimeoutTask = Task.Delay(Timeout).ContinueWith<HttpResponseMessage?>(x => null);
                    HttpResponseMessage? response = await await Task.WhenAny(SendTask, TimeoutTask);
                    return (int?)(response?.StatusCode) ?? -4;//-4 is No Response
                }
                catch (HttpRequestException hre)
                {
                    if (hre.InnerException is AuthenticationException ae) { return -1; }//TLS/Certificate Error or similar
                    if (hre.Message.Contains("Connection refused") ||
                        hre.Message.Contains("machine actively refused it")) { return -2; }//Connection Refused
                    if (hre.Message.Contains("Name does not resolve")) { return -3; }//DNS Error
                    if (hre.Message.Contains("connected host has failed to respond.")) { return -4; }//No Response
                    throw hre;//Rethrow unknown exceptions to be caught later
                }
                catch (OperationCanceledException) { return -4; } //No Response
                #region Unlocking
                finally
                {
                    lock (this)
                    {
                        Ready = true;
                    }
                }
                #endregion
            }
        }
        List<HttpClientWrapper> SecureCheckers = new List<HttpClientWrapper>();
        List<HttpClientWrapper> UnsecureCheckers = new List<HttpClientWrapper>();
        //HttpClientWrapper[] ClientWrappers = new HttpClientWrapper[1000];
        //private int AcceptStart => ClientWrappers.Length / 2;
        public FastHTTPChecker(int Timeout) { this.Timeout = Timeout; }
        public async Task<int> CheckAsync(string uri, bool AcceptAnyCert)
        {
            int? toReturn;
            if (AcceptAnyCert)
            {
                while (true)//This shouldn't actually loop, since we make one for ourselves if we can't use the others, but the engine needs to be sure.
                {
                    for (int i = 0; i < UnsecureCheckers.Count; i++)
                    {
                        toReturn = await UnsecureCheckers[i].CheckAsyncIfReady(uri);
                        if (toReturn != null) { return toReturn.Value; }
                    }
                    HttpClientWrapper toAdd = new HttpClientWrapper(true, Timeout);
                    toReturn = await toAdd.CheckAsyncIfReady(uri);
                    UnsecureCheckers.Add(toAdd);
                    if (toReturn != null) { return toReturn.Value; }
                    await Task.Delay(100);
                }
            }
            else
            {
                while (true)//This shouldn't actually loop, since we make one for ourselves if we can't use the others, but the engine needs to be sure.
                {
                    for (int i = 0; i < SecureCheckers.Count; i++)
                    {
                        toReturn = await SecureCheckers[i].CheckAsyncIfReady(uri);
                        if (toReturn != null) { return toReturn.Value; }
                    }
                    HttpClientWrapper toAdd = new HttpClientWrapper(false, Timeout);
                    toReturn = await toAdd.CheckAsyncIfReady(uri);
                    SecureCheckers.Add(toAdd);
                    if (toReturn != null) { return toReturn.Value; }
                    await Task.Delay(100);
                }
            }
        }
    }
}
