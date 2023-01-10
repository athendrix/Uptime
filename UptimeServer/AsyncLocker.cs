using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace UptimeServer
{
    public static class AsyncLocker
    {
        public static ConcurrentDictionary<object, bool> Locked = new ConcurrentDictionary<object, bool>();
        public static async Task TryLock(object LockObject, Func<Task> ToCall)
        {
            bool havelock;
            lock (LockObject)
            {
                if (!Locked.ContainsKey(LockObject) || Locked[LockObject] == false)
                {
                    Locked[LockObject] = true;
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
                await ToCall();
            }
            finally
            {
                lock (LockObject)
                {
                    Locked[LockObject] = false;
                }
            }
        }
    }
}
