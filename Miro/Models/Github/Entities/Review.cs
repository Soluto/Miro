using System;
using Newtonsoft.Json;

namespace Miro.Models.Github.Entities
{
    public class Review
    {
        public User User { get; set; } = new User();
        public string State { get; set; }
        [JsonProperty(PropertyName = "submitted_at")]
        public DateTime SubmittedAt { get; set; }
    }
}