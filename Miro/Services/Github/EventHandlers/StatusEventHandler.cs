using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miro.Models.Checks;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Checks;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Serilog;

namespace Miro.Services.Github.EventHandlers
{
    public class StatusEventHandler : IWebhookEventHandler<StatusEvent>
    {
        private readonly MergeRequestsRepository mergeRequestsRepository;
        private readonly MergeOperations mergeOperations;
        private readonly ChecksManager checksManager;
        private readonly CommentCreator commentCreator;
        private readonly ILogger logger = Log.ForContext<StatusEventHandler>();

        public StatusEventHandler(MergeRequestsRepository mergeRequestsRepository,
                                      MergeOperations mergeOperations,
                                      ChecksManager checksManager,
                                      CommentCreator commentCreator)
        {
            this.mergeRequestsRepository = mergeRequestsRepository;
            this.mergeOperations = mergeOperations;
            this.checksManager = checksManager;
            this.commentCreator = commentCreator;
        }

        public async Task<WebhookResponse> Handle(StatusEvent payload)
        {
            var sha = payload.Sha;
            var extraLogData = new {payload.Sha, owner = payload.Repository.Owner.Login, repo = payload.Repository.Name};
            logger.WithExtraData(extraLogData).Information($"Received status from branch, handling");
            return await HandleStatus(sha, payload);
        }

        private async Task<WebhookResponse> HandleStatus(string sha, StatusEvent payload)
        {


            var mergeRequest = await mergeRequestsRepository.GetBySha(payload.Repository.Owner.Login, payload.Repository.Name, sha);
            if (mergeRequest == null)
            {
                logger.WithExtraData(new {payload.Sha, owner = payload.Repository.Owner.Login, repo = payload.Repository.Name}).Information("Received status from unknown Pull Request, ignoring");
                return new WebhookResponse(false, "Received status from unknown Pull Request, ignoring");
            }
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;
            var testName = payload.Context;
            var testState = payload.State;
            var targetUrl = payload.TargetUrl;

            if (!await checksManager.IsRequiredCheck(owner, repo, testName))
            {
                logger.WithMergeRequestData(mergeRequest).WithExtraData(new {testName, testState, targetUrl}).Information("Received a non-required status from Pull Request, ignoring");
                return new WebhookResponse(false, "Received a non-required status from Pull Request, ignoring");
            }

            if (isStaleStatusEvent(mergeRequest, sha))
            {
                logger.WithMergeRequestData(mergeRequest).WithExtraData(new {testName, testState, staleSha = sha}).Information("Received stale status from Pull Request, ignoring");
                return new WebhookResponse(false, "Received stale status from Pull Request, ignoring");
            }
            logger.WithMergeRequestData(mergeRequest).WithExtraData(new {testName, testState, staleSha = sha}).Information($"Received status from Pull Request, updating DB");
            var updatedMergeRequest = await mergeRequestsRepository.UpdateCheckStatus(owner, repo, prId, testName, testState, targetUrl);
            if (testState == "success")
            {
                var checks = updatedMergeRequest.Checks?.Select(x => $"{x.Name}_{x.Status}");
                logger.WithMergeRequestData(mergeRequest).WithExtraData(new {testName, testState, staleSha = sha, checks = String.Join(",", checks)}).Information($"Received status from Pull Request, updated DB");
                var merged = await mergeOperations.TryToMerge(updatedMergeRequest);
                return new WebhookResponse(true, $"Received success status from Pull Request, did branch merge: {merged}");
            }
            return new WebhookResponse(true, $"Received {testState} status from Pull Request, handled without trying to merge");
        }

        private bool isStaleStatusEvent(MergeRequest mergeRequest, string sha) => mergeRequest.Sha != null && mergeRequest.Sha != sha;
    }
}