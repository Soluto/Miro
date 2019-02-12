using Newtonsoft.Json;

namespace Miro.Models.Github.RequestPayloads
{
    public class UpdateBranchPayload
    {
        [JsonProperty(PropertyName = "head")]
        public string Head { get; set; }
        
        [JsonProperty(PropertyName = "base")]
        public string Base { get; set; }

        [JsonProperty(PropertyName = "commit_message")]
        public string CommitMessage { get; set; }
    }
}