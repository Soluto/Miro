using System;
using MongoDB.Bson;

namespace Miro.Models.MiroConfig
{
    public class RepoConfig
    {
        public ObjectId Id { get; set; }
        public string Owner { get; set; }
        public string Repo { get; set; }
        public bool DeleteAfterMerge { get; set; } = true;
        public string MergePolicy { get; set; } = "whitelist";
        public string UpdateBranchStrategy { get; set; }  = "oldest";
        public string DefaultBranch { get; set; }  = "master";
        public bool Quiet { get; set; }  = false;
        public DateTime UpdatedAt { get; set; }
    }

    public static class RepoConfigExt
    {

        public static bool IsWhitelistStrict(this RepoConfig repoConfig) => repoConfig.MergePolicy == "whitelist-strict";
        public static bool IsValidMergePolicy(this RepoConfig repoConfig) => repoConfig.MergePolicy == "whitelist" ||repoConfig.MergePolicy ==  "whitelist-strict" || repoConfig.MergePolicy == "blacklist";
        public static bool IsValidUpdateBranchStrategy(this RepoConfig repoConfig) => repoConfig.UpdateBranchStrategy == "oldest" || repoConfig.UpdateBranchStrategy ==  "all" || repoConfig.UpdateBranchStrategy == "none";
        public static bool IsBlacklist(this RepoConfig repoConfig) => repoConfig.MergePolicy == "blacklist";

    }

}