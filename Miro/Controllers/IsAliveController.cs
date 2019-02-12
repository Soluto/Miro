using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Microsoft.Extensions.Logging;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Merge;
using Newtonsoft.Json.Linq;

namespace Miro.Controllers
{

    [Route("api/isAlive")]
    public class IsAliveController : ControllerBase
    {
        
        [HttpGet]
        public bool IsAlive()
        {
            return true;
        }
    }
}
