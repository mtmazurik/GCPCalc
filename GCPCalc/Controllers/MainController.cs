using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GCPCalc.Services;
using Microsoft.AspNetCore.Mvc;

namespace GCPCalc.Controllers
{
    [Route("/billing")]
    [ApiController]
    public class MainController : ControllerBase
    {
        // GET compute data
        [HttpGet("compute")]
        public ActionResult<string> GetComputeData([FromServices]IApiService api)
        {
            return Ok(api.GetBillingComputeData());
        }
    }
}
