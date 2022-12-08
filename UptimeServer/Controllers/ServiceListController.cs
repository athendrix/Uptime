using CSL.SQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using UptimeServer.Data;

namespace UptimeServer.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceListController : ControllerBase
    {

        [HttpGet]
        public async Task<IEnumerable<WebService>> Get()
        {
            ReadOnlyMemory<WebService> toReturn = ServiceTracker.GetServices();
            while(toReturn.Length == 0)
            {
                await Task.Delay(100);
                toReturn = ServiceTracker.GetServices();
            }
            return toReturn.ToArray().OrderBy((srv) => srv.checktime == DateTime.MaxValue).ThenBy((srv) => srv.name);
            
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