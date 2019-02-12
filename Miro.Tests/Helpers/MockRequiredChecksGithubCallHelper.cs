using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests.Helpers
{
    public static class MockRequiredChecksGithubCallHelper
    {
        public static async Task<string> MockRequiredChecks(string owner, string repo, string[] requiredChecks = null, string branch = "master")
        {
               var mockedRequiredTests = new {
                contexts =  requiredChecks ?? new string[]{Consts.TEST_CHECK_A, Consts.TEST_CHECK_B}
            };
            return await MockGithubCall("get", RequiredChecksUrlFor(owner, repo, branch), JsonConvert.SerializeObject(mockedRequiredTests), true);
        }
    }
}