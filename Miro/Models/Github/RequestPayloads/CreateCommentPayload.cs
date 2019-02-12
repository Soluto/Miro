using Newtonsoft.Json;

namespace Miro.Models.Github.RequestPayloads
{
    public class CreateCommentPayload
    {
        [JsonProperty(PropertyName = "body")]
        public string Body;
    }
}