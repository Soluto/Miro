using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests.Helpers
{
    public static class MockMergeGithubCallHelper
    {
        public static Task<string> MockMergeCall(string owner, string repo, int prId, bool success = true)
        {
             var mergeResponse = new
            {
                merged = success
            };
            return MockGithubCall("put", $"{PrUrlFor(owner, repo, prId)}/merge", JsonConvert.SerializeObject(mergeResponse), true);
        }
    }
}