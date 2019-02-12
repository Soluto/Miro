using System;
using System.Collections.Generic;
using System.Linq;
using Miro.Models.Checks;
using MongoDB.Bson;

namespace Miro.Models.Merge
{
    public class MergeRequest
    {
        public ObjectId Id { get; set; }
        public string Owner { get; set; }
        public string Repo { get; set; }
        public int PrId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CheckStatus> Checks { get; set; } = new List<CheckStatus>();
        public bool ReceivedMergeCommand { get; set; }
        public DateTime? ReceivedMergeCommandTimestamp { get; set; }
        public string Branch { get; set; }
        public string State { get; set; }
        public string Sha { get; set; }
        public bool IsFork { get; set; }

        
    }

    public static class MergeRequestExt
    {
        public static bool NoFailingChecks(this MergeRequest mergeRequest) =>  mergeRequest.Checks.All(x => x.Status != "failure" && x.Status != "error");
        public static bool AllPassingChecks(this MergeRequest mergeRequest) =>  mergeRequest.Checks.All(x => x.Status != "success");
    }
}