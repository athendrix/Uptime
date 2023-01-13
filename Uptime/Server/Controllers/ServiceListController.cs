using CSL.SQL;
using Microsoft.AspNetCore.Mvc;
using Uptime.Server.Data;
using Uptime.Shared;
using Uptime.Shared.Data;

namespace Uptime.Server.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceListController : ControllerBase
    {

        [HttpGet]
        public async Task<List<ServiceRecord>> Get()
        {
            List<ServiceRecord> toReturn = ServiceTracker.GetServices();
            while(toReturn.Count == 0)
            {
                await Task.Delay(100);
                toReturn = ServiceTracker.GetServices();
            }
            return toReturn.OrderBy(srv => srv.CheckTime == DateTime.MaxValue).ThenBy((srv) => srv.Name).ToList();
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
                    ServiceTracker.Reset();
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
                    ServiceTracker.Reset();
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
                    ServiceTracker.Reset();
                    return Ok();
                }
            }
            return BadRequest();
        }
    }
}