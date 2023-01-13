using Uptime.Shared.Data;

namespace Uptime.Shared
{
 
    // public record struct ServiceModel
    // {
    //     public ServiceModel(string name, string address, string displayAddress, bool external, string backend, string live, bool trustCert, int checkType, string checkTime, string? ErrorText)
    //     {
    //         Name = name;
    //         Address = address;
    //         DisplayAddress = displayAddress;
    //         External = external;
    //         Backend = backend;
    //         Live = live;
    //         TrustCert = trustCert;
    //         CheckType = checkType;
    //         CheckTime = checkTime;
    //     }
    //     public readonly string Name;
    //     public readonly string Address;
    //     public readonly string DisplayAddress;
    //     public readonly bool External;
    //     public readonly string Backend;
    //     public readonly string Live;
    //     public readonly bool TrustCert;
    //     public readonly int CheckType;
    //     public readonly string CheckTime;
    //     public readonly string? ErrorText;
    // }
    public record ServiceRecord(string Name, string Address, string? DisplayAddress, bool External, string? Backend, string Live, bool TrustCert, CheckType CheckType, DateTime CheckTime, string? ErrorText)
    {
        public static ServiceRecord FromService(Services service) =>
            new ServiceRecord(service.Name, service.Address, service.DisplayAddress, service.External, service.Backend, "*UNTESTED*", service.TrustCertificate, service.CheckType, DateTime.MaxValue, null);
    }
}