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
    public class StatusEventProcesingTests
    {
        const int PR_ID = 3;

        private MergeRequestsCollection mergeRequestsCollection;
        private readonly CheckListsCollection checkListsCollection;
        private readonly RepoConfigurationCollection repoConfigurationCollection;


        public StatusEventProcesingTests() 
        {
            this.repoConfigurationCollection = new RepoConfigurationCollection();
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.checkListsCollection = new CheckListsCollection();

        }

        [Fact]
        public async Task ActionIsCompleted_ConclusionIsNotSuccess_DoNothing()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "failure";
            payload["sha"] = sha;
            payload["context"] = Consts.TEST_CHECK_B;

            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            var mergePrCall = await GetCall(mergePrCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.False(mergePrCall.HasBeenMade, "the pr shouldn't be merged");
        }

        [Fact]
        public async Task ActionIsCompleted_ConclusionIsSuccess_NoMergeCommandGiven_PrIsNotMergable_DoNothing()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = sha;
            payload["context"] = Consts.TEST_CHECK_B;

            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, false, checksAlreadyCompleted, sha);

            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);

            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            var mergePrCall = await GetCall(mergePrCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.False(mergePrCall.HasBeenMade, "the pr shouldn't be merged");
        }

        [Fact]
        public async Task ActionIsCompleted_ConclusionIsSuccess_PendingReviews_PrIsNotMergable_DoNothing()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = sha;
            payload["context"] = Consts.TEST_CHECK_B;

            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var createCommentCallId = await MockGithubCall("post", $"{IssueUrlFor(owner, repo, PR_ID)}/comments", "ok", false);

            var requestedReviewsMockedResponse = new
            {
                teams = Array.Empty<object>(),
                users = new[] { new { login = "itay", id = 3 } }
            };
            await MockReviewGithubCallHelper.MockReviewsResponses(JsonConvert.SerializeObject(requestedReviewsMockedResponse), "[]", owner, repo, PR_ID);

            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            var mergePrCall = await GetCall(mergePrCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.False(mergePrCall.HasBeenMade, "the pr shouldn't be merged");
        }

          [Fact]
        public async Task StatusEvent_NonRequiredStatusCheck_DoNothing()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var someOtherTest = "some-non-required-test";

            // Insert to DB Default checks needed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            // Insert to PR tests completed
            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };
            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            // Generate Status event with succesful test not required
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = sha;
            payload["context"] = someOtherTest;
            
            // Mock Merge call
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            // Action - status event
            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.False(mergePrCall.HasBeenMade, "the pr should not have been merged");

            Assert.DoesNotContain(((BsonArray) mergeRequest["Checks"]), x => x["Name"] == someOtherTest);
            Assert.Contains(((BsonArray) mergeRequest["Checks"]), x => x["Name"] == Consts.TEST_CHECK_A);
        }

         [Fact]
        public async Task StatusEvent_LastCheckIsStaleStatusEvent_DoNotMerge()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var staleSha = Guid.NewGuid().ToString();

            // Insert to DB Default checks needed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            // Insert to PR tests completed
            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };
            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            // Generate Status event with succesful test
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = staleSha;
            payload["context"] = Consts.TEST_CHECK_B;
            
            // Mock Merge call
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            // Action - status event
            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.False(mergePrCall.HasBeenMade, "the pr should not have been merged");
        }

          [Fact]
        public async Task StatusEvent_PendingStatusFollowedBySuccess_MergePr()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Insert to DB Default checks needed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            // Insert to PR tests completed
            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };
            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            
            // Generate Status event with pending test
            var pendingPayload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            pendingPayload["repository"]["name"] = repo;
            pendingPayload["repository"]["owner"]["login"] = owner;
            pendingPayload["state"] = "pending";
            pendingPayload["sha"] = sha;
            pendingPayload["context"] = Consts.TEST_CHECK_B;

            // Generate Status event with succesful test
            var successPayload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            successPayload["repository"]["name"] = repo;
            successPayload["repository"]["owner"]["login"] = owner;
            successPayload["state"] = "success";
            successPayload["sha"] = sha;
            successPayload["context"] = Consts.TEST_CHECK_B;
            
            // Mock Github calls
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

            // Action - status event
            await SendWebhookRequest("status", JsonConvert.SerializeObject(pendingPayload));
            await SendWebhookRequest("status", JsonConvert.SerializeObject(successPayload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.True(mergePrCall.HasBeenMade, "the pr should be merged");
            Assert.True(mergeCommentCall.HasBeenMade, "the merge comment should have been written");
        }


        [Fact]
        public async Task StatusEvent_LastCheckSuccessful_MergePr()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Insert to DB Default checks needed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);

            // Insert to PR tests completed
            var checksAlreadyCompleted = new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                }
            };
            await mergeRequestsCollection.Insert(owner, repo, PR_ID, Consts.DEFAULT_BRANCH, true, checksAlreadyCompleted, sha);

            // Generate Status event with succesful test
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = sha;
            payload["context"] = Consts.TEST_CHECK_B;
            
            // Mock Github calls
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

            // Action - status event
            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.True(mergePrCall.HasBeenMade, "the pr should be merged");
            Assert.True(mergeCommentCall.HasBeenMade, "the merge comment should have been written");
        }
        [Fact]
        public async Task StatusEvent_LastCheckSuccessful_NoMergeCommandGiven_BlackListPolicy_MergePr()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            // Required test is TEST_CHECK_A
            await checkListsCollection.Insert(owner, repo, new string[]{Consts.TEST_CHECK_A});
            await repoConfigurationCollection.Insert(owner, repo, false, "oldest", "blacklist");

            // Create Pull Request
            await OpenBlackListPullRequest(owner, repo, PR_ID, sha);

            // Generate Status event with succesful test
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/Status.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["state"] = "success";
            payload["sha"] = sha;
            payload["context"] = Consts.TEST_CHECK_A;
            
            // Mock Github calls
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

            // Action - status event
            await SendWebhookRequest("status", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();

            Assert.True(mergePrCall.HasBeenMade, "the pr should be merged");
            Assert.True(mergeCommentCall.HasBeenMade, "the merge comment should have been written");
        }


        private async Task OpenBlackListPullRequest(string owner, string repo, int prId, string sha)
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/PullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            await MockCommentGithubCallHelper.MockCommentGithubCallBlackListPrOpened(owner, repo, prId);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["head"]["sha"] = sha;
            payload["pull_request"]["number"] = prId;
            payload["number"] = prId;

            // ACTION
            await SendWebhookRequest("pull_request", JsonConvert.SerializeObject(payload));
        }

    }
}
