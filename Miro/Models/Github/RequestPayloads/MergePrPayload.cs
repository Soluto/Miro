using Newtonsoft.Json;

namespace Miro.Models.Github.RequestPayloads
{
    public class MergePrPayload
    {
        [JsonProperty(PropertyName = "commit_title")]
        public string CommitTitle { get; set; }

        [JsonProperty(PropertyName = "commit_message")]
        public string CommitMessage { get; set; }

        [JsonProperty(PropertyName = "merge_method")]
        public string MergeMethod { get; set; }

        public string Sha { get; set; }
    }
}