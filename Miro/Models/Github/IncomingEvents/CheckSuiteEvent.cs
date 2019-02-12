using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.IncomingEvents
{
    public class CheckSuiteEvent
    {
        public string Action { get; set; }
        [JsonProperty(PropertyName = "check_suite")]
        public CheckSuite CheckSuite { get; set; }

        public Repository Repository { get; set; }
    }
}