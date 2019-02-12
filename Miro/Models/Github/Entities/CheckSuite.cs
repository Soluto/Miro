using System.Collections.Generic;
using Newtonsoft.Json;

namespace Miro.Models.Github.Entities
{
    public class CheckSuite
    {
        public string Status { get; set; }
        public string Conclusion { get; set; }
        [JsonProperty(PropertyName = "pull_requests")]
        public List<PullRequest> PullRequests { get; set; }
    }
}