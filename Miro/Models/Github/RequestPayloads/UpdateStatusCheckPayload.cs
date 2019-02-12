using Newtonsoft.Json;

namespace Miro.Models.Github.RequestPayloads
{
    public class UpdateStatusCheckPayload
    {
        [JsonProperty(PropertyName = "state")]
        public string State { get; set; }
        
        [JsonProperty(PropertyName = "target_url")]
        public string TargetUrl { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "context")]
        public string Context { get; set; }
    }
}