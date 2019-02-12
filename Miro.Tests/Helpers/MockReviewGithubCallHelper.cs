using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests.Helpers
{
    public static class MockReviewGithubCallHelper
    {
        public static async Task MockReviewsResponses(string requestedReviews, string madeReviews, string owner, string repo, int prId)
        {
            await MockGithubCall("get", $"{PrUrlFor(owner, repo, prId)}/requested_reviewers", requestedReviews);
            await MockGithubCall("get", $"{PrUrlFor(owner, repo, prId)}/reviews", madeReviews);
        }

        public static Task MockAllReviewsPassedResponses(string owner, string repo, int prId)
        {
              var requestedReviewsMockedResponse = new
            {
                teams = Array.Empty<object>(),
                users = Array.Empty<object>()
            };
            var body = JsonConvert.SerializeObject(requestedReviewsMockedResponse);

            return MockReviewsResponses(body, "[]", owner, repo, prId);
        }
    }
}