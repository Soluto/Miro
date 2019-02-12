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

namespace Miro.Tests
{
    public class ReviewEventProcessingTests
    {
        const int PR_ID = 8;

        private MergeRequestsCollection mergeRequestsCollection;
        private readonly CheckListsCollection checkListsCollection;

        public ReviewEventProcessingTests() 
        {
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.checkListsCollection = new CheckListsCollection();
        }

         [Fact]
        public async Task ReceievePullRequestReviewChangesRequestedEvent_AllTestsPassed_DoNothing()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/ReviewPullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["number"] = PR_ID;
            payload["review"]["state"] = "CHANGES_REQUESTED";

            // Mock github
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);

            // insert to DB with all checks passed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, PR_ID);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);

            // ACTION
            await SendWebhookRequest("pull_request_review", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergePrCall = await GetCall(mergePrCallId);
            Assert.False(mergePrCall.HasBeenMade, "pr should not have been merged");

            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.NotNull(mergeRequest);
        }

        [Fact]
        public async Task ReceievePullRequestReviewApprovedEvent_AllTestsPassed_MergePr()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();
            var sha = Guid.NewGuid().ToString();

            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/ReviewPullRequest.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["pull_request"]["number"] = PR_ID;

            // Mock github
            var mergeCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallMerging(owner, repo, PR_ID);
            var mergePrCallId = await MockMergeGithubCallHelper.MockMergeCall(owner, repo, PR_ID);
            // var miroMergeCheckCallId = await MockGithubCall("post", StatusCheckUrlFor(owner, repo, sha), "{}", false);
            await MockReviewGithubCallHelper.MockAllReviewsPassedResponses(owner, repo, PR_ID);

            // insert to DB with all checks passed
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccessAndMergeCommand(owner, repo, PR_ID, null, sha);

            // ACTION
            await SendWebhookRequest("pull_request_review", JsonConvert.SerializeObject(payload));

            // ASSERT
            var mergeCommentCall = await GetCall(mergeCommentCallId);
            var mergePrCall = await GetCall(mergePrCallId);
            // var miroMergeCheckCall = await GetCall(miroMergeCheckCallId);
            Assert.True(mergeCommentCall.HasBeenMade, "a merging comment should have been posted to the pr");
            // Assert.True(miroMergeCheckCall.HasBeenMade, "a call to delete miro merge check should have been called");
            Assert.True(mergePrCall.HasBeenMade, "pr should have been merged");

            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstAsync();
            Assert.NotNull(mergeRequest);
        }
    }
}
