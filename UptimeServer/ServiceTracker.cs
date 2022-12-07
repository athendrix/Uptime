using System.Threading.Tasks;

namespace UptimeServer
{
    public class ServiceTracker
    {
        private static ServiceTracker Singleton = new ServiceTracker();
        private Task ServiceTrackingTask;
        private ServiceTracker()
        {

        }
    }
}
