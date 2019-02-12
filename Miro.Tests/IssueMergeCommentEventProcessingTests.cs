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
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.WebhookRequestSender;
using static Miro.Tests.Helpers.GithubUrlHelpers;

namespace Miro.Tests
{
    public class IssueMergeCommentEventProcessingTests
    {
        const int PR_ID = 2;

        private readonly MergeRequestsCollection mergeRequestsCollection;
        private readonly CheckListsCollection checkListsCollection;
        private readonly RepoConfigurationCollection repoConfigurationCollection;

        public IssueMergeCommentEventProcessingTests()
        {
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.checkListsCollection = new CheckListsCollection();
            this.repoConfigurationCollection = new RepoConfigurationCollection();
        }


        [Fact]
        public async Task ReceiveMergeCommand_AllChecksPassed_PrHasPendingReviews_PrNotMerged()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID);
            await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            var requestedReviewsMockedResponse = new
            {
                teams = Array.Empty<object>(),
                users = new[] { new { login = "itay", id = 3 } }
            };

            // Mock github call
            await MockReviewGithubCallHelper.MockReviewsResponses(JsonConvert.SerializeObject(requestedReviewsMockedResponse), "[]", owner, repo, PR_ID);
            var commentPendingReviewsCallId = await MockCommentGithubCallHelper.MockCommentGithubCallPendingReviews(owner, repo, PR_ID, "itay");

            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // ASSERT
            var commentPendingReviewsCall = await GetCall(commentPendingReviewsCallId);
            Assert.True(commentPendingReviewsCall.HasBeenMade, "comment should have been written");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);
        }

        [Fact]
        public async Task ReceiveMergeCommand_AllChecksPassed_PrHasChangesRequested_WriteErrorComment()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID);
             await repoConfigurationCollection.Insert(owner, repo);

            var requestedReviewsMockedResponse = new
            {
                teams = Array.Empty<object>(),
                users = Array.Empty<object>()
            };

            var reviewsMockedResponse = new[]
            {
                new
                {
                    User = new
                    {
                        login = "itay",
                        id = 3
                    },
                    State = "CHANGES_REQUESTED"
                }
            };
            await MockReviewGithubCallHelper.MockReviewsResponses(JsonConvert.SerializeObject(requestedReviewsMockedResponse), JsonConvert.SerializeObject(reviewsMockedResponse), owner, repo, PR_ID);
            var commentChangesRequestedCallId = await MockCommentGithubCallHelper.MockCommentGithubCallRequestedChanges(owner, repo, PR_ID, "itay");
            
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // ASSERT
            var commentChangesRequestedCall = await GetCall(commentChangesRequestedCallId);
            Assert.True(commentChangesRequestedCall.HasBeenMade, "comment should have been written");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);
        }

        

        [Fact]
        public async Task ReceiveMergeCommand_PrIsApprovedByAllReviewersButChecksDidNotFinish_PrIsNotMerged()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Mock DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.Insert(owner, repo, PR_ID);
             await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github Calls
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            var commentMissingChecksCallId = await MockCommentGithubCallHelper.MockCommentGithubCallPendingChecks(owner, repo, PR_ID);


             // ACTION
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // ASSERT
            var commentMissingChecksCall = await GetCall(commentMissingChecksCallId);
            Assert.True(commentMissingChecksCall.HasBeenMade, "comment should have been written");

            var mergePrCall = await GetCall(mergePrCallId);
            Assert.False(mergePrCall.HasBeenMade, "PR should not be merged");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);
        }

          [Fact]
        public async Task ReceiveMergeCommand_MergingFails_StrictMergePolicy_UpdateBranchIsCalled()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var branch = "some_test_branch";

            // Mock DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branch, sha);
            await repoConfigurationCollection.Insert(owner, repo, false, "oldest", "whitelist-strict");

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github Calls
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var commentReadyForMergingCallId = await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);
            var mergeFailedCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMergeFailed(owner, repo, PR_ID);
            var mergePrCallId = await MockGithubCall("put", $"{PrUrlFor(owner, repo, PR_ID)}/merge", null, 409);
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), branch, "ok", false);
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

             // ACTION
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var updateBranchCall = await GetCall(updateBranchCallId);
            var commentReadyForMergingCall = await GetCall(commentReadyForMergingCallId);
            var mergeFailedCommentCall = await GetCall(mergeFailedCommentCallId);
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            Assert.True(mergePrCall.HasBeenMade, "PR should have tried to merge");
            Assert.True(miroMergeCheckCall.HasBeenMade, "miro merge check success call should have been made");
            Assert.True(commentReadyForMergingCall.HasBeenMade, "should get ready for merging comment");
            Assert.True(updateBranchCall.HasBeenMade, "PR should have updated branch");
            Assert.True(mergeFailedCommentCall.HasBeenMade, "should get PR merge failed comment");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] < DateTime.UtcNow);
            Assert.NotNull(mergeRequest);
            Assert.Equal("Miro merge check" , miroMergeCheckCall.Details.Body["context"]);
            Assert.Equal("success" , miroMergeCheckCall.Details.Body["state"]);
        }


         [Fact]
        public async Task ReceiveMergeCommand_MergingFails_UpdateBranchIsCalled()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var branch = "some_test_branch";

            // Mock DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branch, sha);
             await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github Calls
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var commentReadyForMergingCallId = await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);
            var mergeFailedCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMergeFailed(owner, repo, PR_ID);
            var mergePrCallId = await MockGithubCall("put", $"{PrUrlFor(owner, repo, PR_ID)}/merge", null, 409);
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), branch, "ok", false);
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

             // ACTION
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var mergePrCall = await GetCall(mergePrCallId);
            var updateBranchCall = await GetCall(updateBranchCallId);
            var commentReadyForMergingCall = await GetCall(commentReadyForMergingCallId);
            var mergeFailedCommentCall = await GetCall(mergeFailedCommentCallId);
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            Assert.True(mergePrCall.HasBeenMade, "PR should have tried to merge");
            Assert.False(miroMergeCheckCall.HasBeenMade, "miro merge check success call should not have been made in a blacklist merge policy");
            Assert.True(commentReadyForMergingCall.HasBeenMade, "should get ready for merging comment");
            Assert.True(updateBranchCall.HasBeenMade, "PR should have updated branch");
            Assert.True(mergeFailedCommentCall.HasBeenMade, "should get PR merge failed comment");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] < DateTime.UtcNow);
            Assert.NotNull(mergeRequest);
        }


         [Fact]
        public async Task ReceiveMergeCommand_MergingFails_CanNotUpdateBecausePrIsFork()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();
            var branch = "some_test_branch";

            // Mock DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, branch, sha, true); // IsFork = true
            await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            // Mock Github Calls
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);
            await MockCommentGithubCallHelper.MockCommentGithubCallMergeFailed(owner, repo, PR_ID);
            await MockGithubCall("put", $"{PrUrlFor(owner, repo, PR_ID)}/merge", null, 404);
            await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);
            var updateFailedCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallCanNotUpdateBecauseFork(owner, repo, PR_ID);
            var updateBranchCallId = await MockGithubCall("post", MergesUrlFor(owner, repo), branch, "ok", false);

             // ACTION
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var updateBranchCall = await GetCall(updateBranchCallId);
            var updateFailedCommentCall = await GetCall(updateFailedCommentCallId);
            Assert.False(updateBranchCall.HasBeenMade, "PR should not have updated branch");
            Assert.True(updateFailedCommentCall.HasBeenMade, "Can not update comment should have been called");
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.NotNull(mergeRequest);
        }

        [Fact]
        public async Task ReceiveMergeCommand_PrIsApprovedByAllReviewersAndAllChecksPassed_MergePr()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
             await repoConfigurationCollection.Insert(owner, repo);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;


            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, null, sha);

            // Mock github calls
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var commentReadyForMergingCallId = await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);

            // Action
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            var commentReadyForMergingCall = await GetCall(commentReadyForMergingCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergePrCall = await GetCall(mergePrCallId);
            Assert.True(commentReadyForMergingCall.HasBeenMade, "should have receieved a ready for merging comment");
            Assert.True(mergeCommentCall.HasBeenMade, "a merging comment should have been posted to the pr");
            Assert.True(mergePrCall.HasBeenMade, "pr should have been merged");

            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);

        }

        
        [Fact]
        public async Task ReceiveMergeCommand_PrIsApprovedByAllReviewersAndAllChecksPassed_StrictMergePolicy_MergePr()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await repoConfigurationCollection.Insert(owner, repo, false, "oldest", "whitelist-strict");

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;


            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, null, sha);

            // Mock github calls
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var commentReadyForMergingCallId = await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);
            var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);

            // Action
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            var commentReadyForMergingCall = await GetCall(commentReadyForMergingCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergePrCall = await GetCall(mergePrCallId);
            var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            Assert.True(commentReadyForMergingCall.HasBeenMade, "should have receieved a ready for merging comment");
            Assert.True(mergeCommentCall.HasBeenMade, "a merging comment should have been posted to the pr");
            Assert.True(miroMergeCheckCall.HasBeenMade, "a call to miro merge status check should have been called");
            Assert.True(mergePrCall.HasBeenMade, "pr should have been merged");

            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);

            Assert.Equal("Miro merge check" ,miroMergeCheckCall.Details.Body["context"]);
            Assert.Equal("success" ,miroMergeCheckCall.Details.Body["state"]);
        }


         [Fact]
        public async Task ReceiveMergeCommand_PrIsApprovedByAllReviewersAndAllChecksPassed_GetRequiredChecksAndMergePr()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;

            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID, null, sha);
             await repoConfigurationCollection.Insert(owner, repo);

            // Mock Github Calls
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);
            var commentReadyForMergingCallId = await MockCommentGithubCallHelper.MockCommentGithubPRIsReadyForMerging(owner, repo, PR_ID);
            var getRequiredChecksCallId = await MockRequiredChecksGithubCallHelper.MockRequiredChecks(owner, repo, new string[] {Consts.TEST_CHECK_A, Consts.TEST_CHECK_B});

            // Action
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var commentReadyForMergingCall = await GetCall(commentReadyForMergingCallId);
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergePrCall = await GetCall(mergePrCallId);
            var getRequiredChecksCall = await GetCall(getRequiredChecksCallId);
            Assert.True(commentReadyForMergingCall.HasBeenMade, "should have receieved a ready for merging comment");
            Assert.True(mergeCommentCall.HasBeenMade, "a merging comment should have been posted to the pr");
            Assert.True(getRequiredChecksCall.HasBeenMade, "should have fetched required checks");
            Assert.True(mergePrCall.HasBeenMade, "pr should have been merged");

            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID && d["ReceivedMergeCommand"] == true).FirstAsync();
            Assert.NotNull(mergeRequest);
        }
    }
}
