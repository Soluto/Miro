using System;
using System.Threading.Tasks;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Serilog;

namespace Miro.Services.Github.EventHandlers
{
    public class PullRequestReviewEventHandler : IWebhookEventHandler<PullRequestReviewEvent>
    {
         private readonly MergeRequestsRepository mergeRequestsRepository;
        private readonly MergeOperations mergeOperations;
        private readonly CommentCreator commentCreator;
        private readonly ILogger logger = Log.ForContext<PullRequestReviewEventHandler>();

        public PullRequestReviewEventHandler(
            MergeRequestsRepository mergeRequestsRepository,
            MergeOperations mergeOperations,
            CommentCreator commentCreator)
        {
            this.mergeRequestsRepository = mergeRequestsRepository;
            this.mergeOperations = mergeOperations;
            this.commentCreator = commentCreator;
        }

        public async Task<WebhookResponse> Handle(PullRequestReviewEvent payload)
        {
            var submittedAction = payload.Action.Equals("submitted", StringComparison.OrdinalIgnoreCase);
            if (!submittedAction)
            {
                logger.WithExtraData(new {payloadAction = payload.Action}).Information("Received Review event on action other than 'submitted', ignoring");
                return new WebhookResponse(false, "Received Review event on action other than 'submitted', ignoring");
            }

            var isApprovedState = payload.Review.State.Equals("approved", StringComparison.OrdinalIgnoreCase);
            if (!isApprovedState)
            {
                logger.WithExtraData(new {payloadAction = payload.Action, reviewState = payload.Review.State}).Information($"Received submit Review event on event other than 'approved', ignoring");
                return new WebhookResponse(false, "Received submit Review event on action other than 'submitted', ignoring");
            }

            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var prId = payload.PullRequest.Number;

            var mergeRequest = await mergeRequestsRepository.Get(owner, repo, prId);
            if (mergeRequest == null) 
            {
                logger.WithExtraData(new {owner, repo, prId}).Warning("Received Review event on unknown PR, Miro can't handle this");
                return new WebhookResponse(false, "Received Review event on unknown PR, Miro can't handle this");
            }
            
            logger.WithMergeRequestData(mergeRequest).Information("Received Approved Review event on PR, Trying to merge");
            var merged = await mergeOperations.TryToMerge(mergeRequest);
            return new WebhookResponse(true, $"Received Approved Review event on PR, did branch merge: {merged}");
        }
    }
}