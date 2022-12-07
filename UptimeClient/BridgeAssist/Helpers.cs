using System;
using System.Threading.Tasks;
using Bridge;
using Bridge.Html5;

namespace BridgeAssist
{
    class Helpers
    {
        #region Promises
        public static Task<T> MakePromise<T>(Action<Action<T>, Action<Error>> promiseCallback)
        {
            IPromise promise = Script.Call<IPromise>("new Promise", promiseCallback);
            Func<Task<T>> toReturn = async () =>
            {
                return (T)(await Task.FromPromise(promise))[0];
            };
            return toReturn();
        }
        public static Task MakePromise(Action<Action, Action<Error>> promiseCallback)
        {
            IPromise promise = Script.Call<IPromise>("new Promise", promiseCallback);
            Func<Task> toReturn = async () =>
            {
                await Task.FromPromise(promise);
                return;
            };
            return toReturn();
        }
        #endregion
        #region Server Calls
        public static Task<T> Get<T>(string url, string username = "", string password = "")
        {
            return MakePromise<T>((Resolve, Reject) =>
            {
                XMLHttpRequest request = new XMLHttpRequest();
                request.OnReadyStateChange = () =>
                {
                    if (request.ReadyState != AjaxReadyState.Done)
                    {
                        return;
                    }
                    if (((request.Status == 200) || (request.Status == 304)) && Generics.TryParse(request.ResponseText, out T result))
                    {
                        Resolve(result);
                    }
                    else
                    {
                        Reject(new Error() { Message = request.ResponseText });
                    }
                };
                request.Open("GET", url, true);
                if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
                {
                    request.SetRequestHeader("Authorization", "Basic " + Window.Btoa(username + ":" + password));
                }
                else if (!string.IsNullOrWhiteSpace((string)Window.LocalStorage["token"]))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + (string)Window.LocalStorage["token"]);
                }
                request.Send();
            });
        }
        public static Task<bool> Check(string url)
        {
            return MakePromise<bool>((Resolve, Reject) =>
            {
                XMLHttpRequest request = new XMLHttpRequest();
                request.OnReadyStateChange = () =>
                {
                    if (request.ReadyState != AjaxReadyState.Done)
                    {
                        return;
                    }
                    if (((request.Status == 200) || (request.Status == 304)))
                    {
                        Resolve(true);
                    }
                    else
                    {
                        Resolve(false);
                    }
                };
                request.Open("GET", url, true);
                request.Send();
            });
        }
        public static Task<T> Post<T,U>(string url, U data, string username = "", string password = "")
        {
            return MakePromise<T>((Resolve, Reject) =>
            {
                string stringdata = Generics.ToString(data);
                XMLHttpRequest request = new XMLHttpRequest();
                request.OnReadyStateChange = () =>
                {
                    if (request.ReadyState != AjaxReadyState.Done)
                    {
                        return;
                    }
                    if (((request.Status == 200) || (request.Status == 304)) && Generics.TryParse(request.ResponseText, out T result))
                    {
                        Resolve(result);
                    }
                    else
                    {
                        Reject(new Error() { Message = request.ResponseText });
                    }
                };
                request.Open("POST", url, true);
                if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
                {
                    request.SetRequestHeader("Authorization", "Basic " + Window.Btoa(username + ":" + password));
                }else if(!string.IsNullOrWhiteSpace((string)Window.LocalStorage["token"]))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + (string)Window.LocalStorage["token"]);
                }
                if(stringdata != null)
                {
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.Send(stringdata);
            });
        }
        public static Task<T> Put<T, U>(string url, U data, string username = "", string password = "")
        {
            return MakePromise<T>((Resolve, Reject) =>
            {
                string stringdata = Generics.ToString(data);
                XMLHttpRequest request = new XMLHttpRequest();
                request.OnReadyStateChange = () =>
                {
                    if (request.ReadyState != AjaxReadyState.Done)
                    {
                        return;
                    }
                    if (((request.Status == 200) || (request.Status == 304)) && Generics.TryParse(request.ResponseText, out T result))
                    {
                        Resolve(result);
                    }
                    else
                    {
                        Reject(new Error() { Message = request.ResponseText });
                    }
                };
                request.Open("PUT", url, true);
                if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
                {
                    request.SetRequestHeader("Authorization", "Basic " + Window.Btoa(username + ":" + password));
                }
                else if (!string.IsNullOrWhiteSpace((string)Window.LocalStorage["token"]))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + (string)Window.LocalStorage["token"]);
                }
                if (stringdata != null)
                {
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.Send(stringdata);
            });
        }
        public static Task<T> Delete <T, U>(string url, U data, string username = "", string password = "")
        {
            return MakePromise<T>((Resolve, Reject) =>
            {
                string stringdata = Generics.ToString(data);
                XMLHttpRequest request = new XMLHttpRequest();
                request.OnReadyStateChange = () =>
                {
                    if (request.ReadyState != AjaxReadyState.Done)
                    {
                        return;
                    }
                    if (((request.Status == 200) || (request.Status == 304)) && Generics.TryParse(request.ResponseText, out T result))
                    {
                        Resolve(result);
                    }
                    else
                    {
                        Reject(new Error() { Message = request.ResponseText });
                    }
                };
                request.Open("DELETE", url, true);
                if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
                {
                    request.SetRequestHeader("Authorization", "Basic " + Window.Btoa(username + ":" + password));
                }
                else if (!string.IsNullOrWhiteSpace((string)Window.LocalStorage["token"]))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + (string)Window.LocalStorage["token"]);
                }
                if (stringdata != null)
                {
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.Send(stringdata);
            });
        }
        #endregion
        #region Local Storage
        public static T GetLocal<T>(string key)
        {
            object localvalue = Window.LocalStorage.GetItem(key);
            string localvaluestring;
            if(localvalue == null)
            {
                localvaluestring = null;
            }
            else
            {
                localvaluestring = (String)localvalue;
            }
            if(Generics.TryParse(localvaluestring, out T toReturn))
            {
                return toReturn;
            }
            return default(T);
        }
        public static void SetLocal<T>(string key, T value)
        {
            if(value == null)
            {
                Window.LocalStorage.RemoveItem(key);
                return;
            }
            Window.LocalStorage.SetItem(key, Generics.ToString(value));
        }
        #endregion
    }
}