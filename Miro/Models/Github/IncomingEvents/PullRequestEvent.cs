using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.IncomingEvents
{
    public class PullRequestEvent
    {
        public string Action { get; set; }
        public int Number { get; set; }

        [JsonProperty(PropertyName = "pull_request")]
        public PullRequest PullRequest { get; set; }
        public Repository Repository { get; set; }
    }
}