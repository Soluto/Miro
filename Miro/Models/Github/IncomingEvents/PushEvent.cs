using System.Collections.Generic;
using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.IncomingEvents
{
    public class PushEvent
    {
        public string Ref { get; set; }
        public string After { get; set; }
        public Repository Repository { get; set; }
    }
}