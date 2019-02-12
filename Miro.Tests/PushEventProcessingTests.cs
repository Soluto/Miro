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
    public class PushEventProcessingTests
    {
        const int PR_ID = 5;

        private readonly CheckListsCollection checkListsCollection;
        private MergeRequestsCollection mergeRequestsCollection;
        private readonly RepoConfigurationCollection repoConfigurationCollection;

        public PushEventProcessingTests() 
        {
            this.checkListsCollection = new CheckListsCollection();
            this.mergeRequestsCollection = new MergeRequestsCollection();
             this.repoConfigurationCollection = new RepoConfigurationCollection();
        }


         [Fact]
        public async Task PushEvent_OnPullRequestBranch_RemoveChecks()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branchName = Guid.NewGuid().ToString();
            var oldSha = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            
            // Load checks in DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branchName, oldSha);

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["ref"] = $"refs/heads/{branchName}";
            payload["after"] = sha;
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Action
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // Assert
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.Empty((BsonArray) mergeRequest["Checks"]);
            Assert.Equal(sha, mergeRequest["Sha"]);
        }

        [Fact]
        public async Task PushEvent_OnPullRequestBranch_StrictMergePolicy_RemoveChecksAndResolveMiroCheck()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branchName = Guid.NewGuid().ToString();
            var oldSha = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            
            // Load checks in DB
            await repoConfigurationCollection.Insert(owner, repo, false, "oldest", "whitelist-strict");
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, PR_ID, branchName, oldSha);

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["ref"] = $"refs/heads/{branchName}";
            payload["after"] = sha;
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock github call
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);
            
            // Action
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // Assert
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.True(miroMergeCheckCall.HasBeenMade, "call to resolve miro check should have been made");
            Assert.Empty((BsonArray) mergeRequest["Checks"]);
            Assert.Equal(sha, mergeRequest["Sha"]);
        }

         [Fact]
        public async Task MultiplePullRequests_PushEventOnMaster_NextPrDidntGiveMergeCommand_NextNextPrBranchIsUpdated()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, secondPrId, secondBranchName);

            // Make second PR older than the first PR
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, false, DateTime.UtcNow.AddMinutes(-10));
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, thirdPrId, true, DateTime.UtcNow.AddMinutes(-5));

            // Mock Github call
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            var getConfigFileCallId = await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "strategyOldest.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            var getConfigFileCall = await GetCall(getConfigFileCallId);
            Assert.True(updateBranchCall.HasBeenMade, "update branch call should have been made on the third branch");
            Assert.True(getConfigFileCall.HasBeenMade, "should fetch new config");
            Assert.Equal(thirdBranchName, updateBranchCall.Details.Body["base"]);
        }


         [Fact]
        public async Task MultiplePullRequests_PushEventOnMaster_NextPrIsMerging_NextNextPrBranchIsUpdated()
        {
             var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, secondPrId, secondBranchName);

            // Make second PR older than the first PR
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, true, DateTime.UtcNow.AddMinutes(-10));
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, thirdPrId, true, DateTime.UtcNow.AddMinutes(-5));

            // Make second PR in the middle of a merge
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, "State", "MERGED");

            // Mock Github call
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "strategyOldest.yml");
           await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);
            
            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            Assert.True(updateBranchCall.HasBeenMade, "update branch call should have been made on the third branch");
            Assert.Equal(thirdBranchName, updateBranchCall.Details.Body["base"]);
        }


        [Fact]
        public async Task MultiplePullRequests_PushEventOnMaster_NextPrHasFailingTests_NextNextPrBranchIsUpdated()
        {
             var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            var secondPrCheckStatus = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "failure"
                },
                new CheckStatus {
                Name = Consts.TEST_CHECK_B,
                Status = "success"
                }
            };


            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.Insert(owner, repo, secondPrId, secondBranchName, true, secondPrCheckStatus);

            // Make second PR older than the first PR
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, true, DateTime.UtcNow.AddMinutes(-10));
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, thirdPrId, true, DateTime.UtcNow.AddMinutes(-5));

            // Mock Github cal
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "strategyOldest.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);
            
            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            Assert.True(updateBranchCall.HasBeenMade, "update branch call should have been made on the third branch");
            Assert.Equal(thirdBranchName, updateBranchCall.Details.Body["base"]);
        }


        [Fact]
        public async Task MultiplePullRequests_PushEventOnMaster_UpdateBranchIsCalledOnNextPr()
        {
             var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, secondPrId, secondBranchName);

            // Make second PR older than the first PR
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, true, DateTime.UtcNow.AddMinutes(-10));
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, thirdPrId, true, DateTime.UtcNow.AddMinutes(-5));

            // Mock Github cal
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "strategyOldest.yml");
           await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            Assert.True(updateBranchCall.HasBeenMade, "update branch call should have been made on the second branch");
            Assert.Equal(secondBranchName, updateBranchCall.Details.Body["base"]);
        }


        [Fact]
        public async Task MultiplePullRequests_PushEventOnDefaultBranch_UpdateBranchIsCalledOnNextPr()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;
            var defaultBranchName = "some-default-branch";

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["ref"] = defaultBranchName;
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;


            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, secondPrId, secondBranchName);

            // Make second PR older than the first PR
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, secondPrId, true, DateTime.UtcNow.AddMinutes(-10));
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, thirdPrId, true, DateTime.UtcNow.AddMinutes(-5));

            // Mock Github call
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "defaultBranchSomeDefaultBranch.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo, null, defaultBranchName);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            Assert.True(updateBranchCall.HasBeenMade, "update branch call should have been made on the second branch");
            Assert.Equal(secondBranchName, updateBranchCall.Details.Body["base"]);
            Assert.Equal(defaultBranchName, updateBranchCall.Details.Body["head"]);
        }


         [Fact]
        public async Task MultiplePullRequests_PushEventOnMaster_ConfigIsSetToNone_NoBranchIsUpdated()
        {
             var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var secondBranchName = Guid.NewGuid().ToString();
            var thirdBranchName = Guid.NewGuid().ToString();
            var secondPrId = 100;
            var thirdPrId = 101;

            // Issue Push event
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Push.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Insert Merge Requests
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, thirdPrId, thirdBranchName);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, secondPrId, secondBranchName);

            // Mock Github cal
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), "ok", false);
            await MockRepoConfigGithubCallHelper.MockRepoConfigGithubCall(owner, repo, "strategyNone.yml");
            await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo);

            // ACTION
            await SendWebhookRequest("push", JsonConvert.SerializeObject(payload));

            // ASSERT
            var updateBranchCall = await GetCall(updateBranchCallId);
            Assert.False(updateBranchCall.HasBeenMade, "update branch call should not have been made");
        }

    }
}
