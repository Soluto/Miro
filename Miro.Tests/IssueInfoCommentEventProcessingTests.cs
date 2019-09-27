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
    public class IssueInfoCommentEventProcessingTests
    {
        const int PR_ID = 7;

        private readonly MergeRequestsCollection mergeRequestsCollection;
        private readonly CheckListsCollection checkListsCollection;


        public IssueInfoCommentEventProcessingTests()
        {
            this.mergeRequestsCollection = new MergeRequestsCollection();
            this.checkListsCollection = new CheckListsCollection();
        }

        [Fact]
        public async Task ReceiveInfoCommand_AllChecksPassed_PrHasPendingReviews_WriteErrorComment()
        {
            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            // Insert Checkslist and PR to DB
            await checkListsCollection.InsertWithDefaultChecks(owner, repo);
            await mergeRequestsCollection.InsertWithTestChecksSuccess(owner, repo, PR_ID);

            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["issue"]["number"] = PR_ID;
            payload["comment"]["body"] = "Miro info";

            var requestedReviewsMockedResponse = new
            {
                teams = Array.Empty<object>(),
                users = new[] { new { login = "itay", id = 3 } }
            };

            // Mock Github Calls
            await MockReviewGithubCallHelper.MockReviewsResponses(JsonConvert.SerializeObject(requestedReviewsMockedResponse), "[]", owner, repo, PR_ID);
            var failureCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallPendingReviews(owner, repo, PR_ID, "itay");

            // ACTION
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // ASSERT
            var failureCommentCall = await GetCall(failureCommentCallId);
            Assert.True(failureCommentCall.HasBeenMade, "Should have recieved a failure comment");
        }
    }
}
