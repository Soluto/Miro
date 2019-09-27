using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests.Helpers
{
    public static class MockCommentGithubCallHelper
    {
         public static Task<string> MockCommentGithubCall(string owner, string repo, int prId, string comment = null)
        {
            return MockGithubCall("post", $"{IssueUrlFor(owner,repo, prId)}/comments", comment, "ok", false);
        }

         public static Task<string> MockCommentGithubCallMerging(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Merging");
        }
         public static Task<string> MockCommentGithubCallBlackListPrOpened(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "This Pull Request will be merged automatically by Miro");
        }
         public static Task<string> MockCommentGithubCallMergeFailed(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Merge failed");
        }
        
          public static Task<string> MockCommentGithubCallCanNotUpdateBecauseFork(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Sorry, Miro doesn't know how to update a fork respository yet");
        }

         public static Task<string> MockCommentGithubCallCancel(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Cancel");
        }

         public static Task<string> MockCommentGithubCallWIP(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Work in Progress");
        }
        
        public static Task<string> MockCommentGithubCallPendingReviews(string owner, string repo, int prId, string reviewer)
        {
            return MockCommentGithubCall(owner, repo, prId, reviewer);
        }

        public static Task<string> MockCommentGithubCallPendingChecks(string owner, string repo, int prId)
        {
            return MockCommentGithubCall(owner, repo, prId, "Missing status checks");
        }

         public static Task<string> MockCommentGithubCallRequestedChanges(string owner, string repo, int prId, string changeRequestedBy)
        {
            return MockCommentGithubCall(owner, repo, prId, changeRequestedBy);
        }
    }
}