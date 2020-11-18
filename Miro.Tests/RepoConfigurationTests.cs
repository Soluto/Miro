using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Miro.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Xunit;
using static Miro.Tests.Helpers.WebhookRequestSender;
using static Miro.Tests.Helpers.GithubUrlHelpers;
using static Miro.Tests.Helpers.GithubApiMock;
using System.Collections.Generic;

namespace Miro.Tests
{
    public class RepoConfigurationTests
    {
        const int PR_ID = 9;

        private readonly CheckListsCollection checkListsCollection;
        private MergeRequestsCollection mergeRequestsCollection;
        private RepoConfigurationCollection repoConfigurationCollection;

        public RepoConfigurationTests() 
        {
            this.checkListsCollection = new CheckListsCollection();
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.repoConfigurationCollection = new RepoConfigurationCollection();
        }


        [Fact]
        public async Task PushEventOnMaster_RepoConfigIsCreated()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github call
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "default.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(getConfigFileCall.HasBeenMade, "getConfigFile call should have been made");

            var repoConfig = await repoConfigurationCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo).FirstAsync();
            Assert.NotNull(repoConfig);
            Assert.Equal("all", repoConfig["UpdateBranchStrategy"]);
            Assert.Equal("whitelist-strict", repoConfig["MergePolicy"]);
        }

          [Fact]
        public async Task PushEventOnMaster_RepoConfigIsCreated_QuietAdded()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github call
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "quiet.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(getConfigFileCall.HasBeenMade, "getConfigFile call should have been made");

            var repoConfig = await repoConfigurationCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo).FirstAsync();
            Assert.NotNull(repoConfig);
            Assert.Equal("all", repoConfig["UpdateBranchStrategy"]);
            Assert.Equal("whitelist-strict", repoConfig["MergePolicy"]);
            Assert.True(repoConfig["quiet"]);
        }
        

        [Fact]
        public async Task PushEventOnMaster_RepoConfigIsCreated_InvalidYml_UseDefaults()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github call
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "invalid.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(getConfigFileCall.HasBeenMade, "getConfigFile call should have been made");

            var repoConfig = await repoConfigurationCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo).FirstAsync();
            Assert.NotNull(repoConfig);
            Assert.Equal("oldest", repoConfig["UpdateBranchStrategy"]);
            Assert.Equal("whitelist", repoConfig["MergePolicy"]);
        }
        
        
        [Fact]
        public async Task PushEventOnDefaultBranch_RepoConfigIsCreated()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["ref"] = "other";
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github call
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "defaultBranchOther.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo, null, "other");

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(getConfigFileCall.HasBeenMade, "getConfigFile call should have been made");

            var repoConfig = await repoConfigurationCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo).FirstAsync();
            Assert.NotNull(repoConfig);
            Assert.Equal("other", repoConfig["DefaultBranch"]);
        }

         [Fact]
        public async Task PushEventOnMaster_NoConfigFilePresent_UseDefaults()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github call
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockFailingRepoConfigCall(owner, repo);
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(getConfigFileCall.HasBeenMade, "getConfigFile call should have been made");

            var repoConfig = await repoConfigurationCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo).FirstAsync();
            Assert.NotNull(repoConfig);
            Assert.Equal("oldest", repoConfig["UpdateBranchStrategy"]);
            Assert.Equal("whitelist", repoConfig["MergePolicy"]);
        }
    }
}
