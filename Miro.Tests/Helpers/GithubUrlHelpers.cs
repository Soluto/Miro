namespace Miro.Tests.Helpers
{
    public static class GithubUrlHelpers
    {
        public static string IssueUrlFor(string owner, string repo, int prId)
        {
            return $"/repos/{owner}/{repo}/issues/{prId}";
        }

        public static string PrUrlFor(string owner, string repo, int prId)
        {
            return $"/repos/{owner}/{repo}/pulls/{prId}";
        }

         public static string MergesUrlFor(string owner, string repo)
        {
            return $"/repos/{owner}/{repo}/merges";
        }

         public static string GetConfigFileFor(string owner, string repo)
        {
            return $"/repos/{owner}/{repo}/contents/.miro.yml";
        }

        public static string RequiredChecksUrlFor(string owner, string repo, string branch = "some-branch")
        {
            return $"/repos/{owner}/{repo}/branches/{branch}/protection/required_status_checks";
        }
        public static string StatusCheckUrlFor(string owner, string repo, string branchOrSha)
        {
            return $"/repos/{owner}/{repo}/statuses/{branchOrSha}";
        }

         public static string DeleteBranchUrlFor(string owner, string repo, string branch)
        {
            return $"/repos/{owner}/{repo}/git/refs/heads/{branch}";
        }
    }
}