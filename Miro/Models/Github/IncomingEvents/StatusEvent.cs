using System.Collections.Generic;
using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.IncomingEvents
{
    public class StatusEvent
    {
        public string Sha { get; set; }
        public Repository Repository { get; set; }
        public string State { get; set; }
        public string Context { get; set; }
        
        [JsonProperty(PropertyName = "target_url")]
        public string TargetUrl { get; set; }
        public List<Branch> Branches { get; set; }
    }
}