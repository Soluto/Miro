using Newtonsoft.Json;

namespace Miro.Models.Github.RequestPayloads
{
    public class PullRequestCheckStatus
    {
        public string State { get; set; }
        public string Context { get; set; }
        
        [JsonProperty(PropertyName = "target_url")]
        public string TargetUrl { get; set; }
    }
}