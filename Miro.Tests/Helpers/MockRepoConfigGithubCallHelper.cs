using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests.Helpers
{
    public static class MockRepoConfigGithubCallHelper
    {
         public static async Task<string> MockRepoConfigGithubCall(string owner, string repo, string configPath)
        {
            var configString = await File.ReadAllTextAsync($"../../../DummyConfigYamls/{configPath}");
            byte[] textAsBytes = Encoding.UTF8.GetBytes(configString);
            var content = Convert.ToBase64String(textAsBytes);
            var configFileResponse = new
            {
                content
            };
            return await MockGithubCall("get", GetConfigFileFor(owner, repo), JsonConvert.SerializeObject(configFileResponse), true);
        }

        public static async Task<string> MockFailingRepoConfigCall(string owner, string repo)
        {
            return await MockGithubCall("get", GetConfigFileFor(owner, repo), null, 404);
        }
    }
}