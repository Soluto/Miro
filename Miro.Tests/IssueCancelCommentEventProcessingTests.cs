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
using static Miro.Tests.Helpers.GithubApiMock;
using static Miro.Tests.Helpers.GithubUrlHelpers;
namespace Miro.Tests
{
    public class IssueCancelCommentEventProcessingTests
    {
        const int PR_ID = 6;

        private MergeRequestsCollection mergeRequestsCollection;

        public IssueCancelCommentEventProcessingTests() 
        {
            this.mergeRequestsCollection = new MergeRequestsCollection();
        }

        [Fact]
        public async Task ReceiveCancelCommand_PrExists_MergeCommandIsRemoved()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            // Issue cancel comment
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["issue"]["number"] = PR_ID;
            payload["comment"]["body"] = "miro cancel";

            // Insert PR
            await mergeRequestsCollection.Insert(owner, repo, PR_ID);

            // Update with MergeCommand
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, PR_ID, true, DateTime.UtcNow);

            // Mock Comments
            var createCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallCancel(owner, repo, PR_ID);

            // Action
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.NotNull(mergeRequest);

            Assert.False((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] == DateTime.MaxValue);

            var createCommentCall = await GetCall(createCommentCallId);
            Assert.True(createCommentCall.HasBeenMade, "a cancel comment should have been posted to the pr");
        }
        [Fact]
        public async Task ReceiveWIPCommand_PrExists_MergeCommandIsRemoved()
        {
            var owner = Guid.NewGuid().ToString();
            var repo = Guid.NewGuid().ToString();

            var payloadString = await File.ReadAllTextAsync("../../../DummyEvents/IssueComment.json");
            var payload = JsonConvert.DeserializeObject<dynamic>(payloadString);

            // Issue cancel comment
            payload["repository"]["name"] = repo;
            payload["repository"]["owner"]["login"] = owner;
            payload["issue"]["number"] = PR_ID;
            payload["comment"]["body"] = "miro wip";

            // Insert PR
            await mergeRequestsCollection.Insert(owner, repo, PR_ID);

            // Update with MergeCommand
            await mergeRequestsCollection.UpdateMergeRequest(owner, repo, PR_ID, true, DateTime.UtcNow);

            // Mock Comments
            var createCommentCallId = await MockCommentGithubCallHelper.MockCommentGithubCallWIP(owner, repo, PR_ID);

            // Action
            await SendWebhookRequest("issue_comment", JsonConvert.SerializeObject(payload));

            // Assert
            var mergeRequest = await mergeRequestsCollection.Collection.Find(d => d["Owner"] == owner && d["Repo"] == repo && d["PrId"] == PR_ID).FirstOrDefaultAsync();
            Assert.NotNull(mergeRequest);

            Assert.False((bool) mergeRequest["ReceivedMergeCommand"]);
            Assert.True(mergeRequest["ReceivedMergeCommandTimestamp"] == DateTime.MaxValue);

            var createCommentCall = await GetCall(createCommentCallId);
            Assert.True(createCommentCall.HasBeenMade, "a cancel comment should have been posted to the pr");
        }
    }
}
