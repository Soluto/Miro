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
    public class PullRequestEventProcessingTests
    {
        const int PR_ID = 1;

        private MergeRequestsCollection mergeRequestsCollection;
        private readonly CheckListsCollection checkListsCollection;
        private RepoConfigurationCollection repoConfigurationCollection;

        public PullRequestEventProcessingTests() 
        {
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.checkListsCollection = new CheckListsCollection();
            this.repoConfigurationCollection = new RepoConfigurationCollection();
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsOpened_SaveANewMergeRequestWithStatusPending()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.False((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] == DateTime.MaxValue);
            
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsOpened_DefaultBranchIsDifferent_Ignore()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await repoConfigurationCollection.Insert(owner, repo, "oldest", "whitelist-strict", "production");

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.Null(mergeRequest);
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsOpened_StrictMergePolicy_SaveANewMergeRequestWithStatusPending()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await repoConfigurationCollection.Insert(owner, repo, "oldest", "whitelist-strict");

            // Mock Github call to add miro merge status check
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "pending", "{}", false);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.False((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(miroMergeCheckCall.HasBeenMade, "should have gotten a call to add the miro check");
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] == DateTime.MaxValue);

            Assert.Equal("Miro merge check" ,miroMergeCheckCall.Details.Body["context"]);
            Assert.Equal("pending" , miroMergeCheckCall.Details.Body["state"]);
            
        }
        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsOpened_BlacklistMergePolicy_SaveANewMergeRequestWithMergeCommandGiven()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await repoConfigurationCollection.Insert(owner, repo, "oldest", "blacklist");

            // Mock Github call to add miro merge status check
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "pending", "{}", false);
            var blackListCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallBlackListPrOpened(owner, repo, PR_ID);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            var blackListCommentCall = await GetCall(blackListCommentCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.True((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(blackListCommentCall.HasBeenMade, "should have gotten a comment about blacklist Pullrequest");
            Assert.False(miroMergeCheckCall.HasBeenMade, "should not have gotten a call to add the miro check");
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] <= DateTime.UtcNow);
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsOpened_BlacklistMergePolicyAndWipInTitle_CommentToUser()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var title = "[WIP] title of PR";

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await repoConfigurationCollection.Insert(owner, repo, "oldest", "blacklist");

            // Mock Github call to add miro merge status check
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "pending", "{}", false);
            var blackListCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallBlackListPrOpened(owner, repo, PR_ID);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;
            payload["pull_request"]["title"] = title;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            var blackListCommentCall = await GetCall(blackListCommentCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.False((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(blackListCommentCall.HasBeenMade, "should have gotten a comment about blacklist Pullrequest");
            Assert.False(miroMergeCheckCall.HasBeenMade, "should not have gotten a call to add the miro check");
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsClosedAndPrMerged_DeleteEntityAndDeleteBranch()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branchName = Guid.NewGuid().ToString();

            // Insert Merge Request
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branchName);
            await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "closed";
            payload["pull_request"]["merged"] = true;
            payload["pull_request"]["head"]["ref"] = branchName;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.Null(mergeRequest);
        }

         [Fact]
        public async Task ReceivePullRequestEvent_ActionIsClosedAndPrMerged_ConfigSaysNotToDelete_DeleteEntityDoNotDeleteBranch()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branchName = Guid.NewGuid().ToString();

            // Insert Merge Request
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branchName);
            await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "closed";
            payload["pull_request"]["merged"] = true;
            payload["pull_request"]["head"]["ref"] = branchName;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.Null(mergeRequest);
        }


        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsSynchronize_UpdateShaAndRemoveChecks()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branchName = Guid.NewGuid().ToString();
            var oldSha = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Insert Merge Request
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branchName, oldSha);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "synchronize";
            payload["pull_request"]["head"]["ref"] = branchName;
            payload["pull_request"]["head"]["sha"] = sha;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.NotNull(mergeRequest);
            Assert.Equal(sha, mergeRequest["Sha"]);
        }


        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsReopened_PRHasLatestChecks()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branch = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Insert Merge Request
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "reopened";
            payload["pull_request"]["head"]["ref"] = branch;
            payload["pull_request"]["head"]["sha"] = sha;

             var prStatusCheckMockedResponse = new[]
            {
                new
                {
                    Context = Consts.TEST_CHECK_A,
                    State = "success"
                },
                 new
                {
                    Context = Consts.TEST_CHECK_B,
                    State = "success"
                }
            };

            // Mock github calls
            var miroGetPrStatusChecksCallId = await MockGithubCall("get", StatusCheckUrlFor(owner, repo, sha), JsonConvert.SerializeObject(prStatusCheckMockedResponse));
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);

            // Action
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // Assert
            var miroGetPrStatusChecksCall = await GetCall(miroGetPrStatusChecksCallId);
            Assert.True(miroGetPrStatusChecksCall.HasBeenMade, "get PR status checks call should have been made");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.NotNull(mergeRequest);

            Assert.True(((BsonArray) mergeRequest["Checks"]).Any(x => x["Name"] == Consts.TEST_CHECK_A && x["Status"] == "success"), $"MergeRequest should contain {Consts.TEST_CHECK_A}");
            Assert.True(((BsonArray) mergeRequest["Checks"]).Any(x => x["Name"] == Consts.TEST_CHECK_B && x["Status"] == "success"),  $"MergeRequest should contain {Consts.TEST_CHECK_B}");
        }


        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsReopened_UnRequiredTestAppears_PrHasOnlyRelevantTests()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var branch = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Insert Merge Request
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "reopened";
            payload["pull_request"]["head"]["ref"] = branch;
            payload["pull_request"]["head"]["sha"] = sha;

             var prStatusCheckMockedResponse = new[]
            {
                new
                {
                    Context = Consts.TEST_CHECK_A,
                    State = "success"
                },
                 new
                {
                    Context = "some-other-test",
                    State = "success"
                }
            };

            // Mock github calls
            var miroGetPrStatusChecksCallId = await MockGithubCall("get", StatusCheckUrlFor(owner, repo, sha), JsonConvert.SerializeObject(prStatusCheckMockedResponse));
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            // Action
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // Assert
            var miroGetPrStatusChecksCall = await GetCall(miroGetPrStatusChecksCallId);
            Assert.True(miroGetPrStatusChecksCall.HasBeenMade, "get PR status checks call should have been made");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.NotNull(mergeRequest);


            Assert.DoesNotContain(((BsonArray) mergeRequest["Checks"]), x => x["Name"] == "some-other-test");
            Assert.Contains(((BsonArray) mergeRequest["Checks"]), x => x["Name"] == Consts.TEST_CHECK_A && x["Status"] == "success");
        }

        [Fact]
        public async Task ReceivePullRequestEvent_ActionIsClosed_DeleteEntity()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Insert Merge Request
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["action"] = "closed";
            payload["pull_request"]["merged"] = false;

            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));

            // Assert
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.Null(mergeRequest);
        }
    }
}
