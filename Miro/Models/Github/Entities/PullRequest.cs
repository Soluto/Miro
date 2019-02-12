using System;
using Newtonsoft.Json;

namespace Miro.Models.Github.Entities
{
    public class PullRequest
    {
        public int Number { get; set; }
        public string State { get; set; }
        public string Title { get; set; }
        public User User { get; set; }
        public Head Head { get; set; }
        public Base Base { get; set; }
        public bool Merged { get; set; }
        
        [JsonProperty(PropertyName = "created_at")]
        public DateTime CreatedAt { get; set; }
    }
}