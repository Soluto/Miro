using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Merge;
using Miro.Models.Checks;
using Miro.Services.Merge;
using Miro.Services.Checks;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using Miro.Services.Logger;
using Miro.Services.Comments;
using Miro.Models.Github.Responses;
using Miro.Services.MiroConfig;
using Miro.Models.MiroConfig;
using System.Text.RegularExpressions;

namespace Miro.Services.Github.EventHandlers
{
    public class IssueCommentEventHandler : IWebhookEventHandler<IssueCommentEvent>
    {
        private readonly CommentCreator commentCreator;
        private readonly MergeRequestsRepository mergeRequestRepository;
        private readonly MergeabilityValidator mergeabilityValidator;
        private readonly RepoConfigManager repoConfigManager;
        private readonly MiroMergeCheck miroMergeCheck;
        private readonly MergeOperations mergeOperations;
        private readonly ILogger logger = Log.ForContext<IssueCommentEventHandler>();

        public IssueCommentEventHandler(
                                        CommentCreator commentCreator,
                                        MergeRequestsRepository mergeRequestRepository,
                                        MergeabilityValidator mergeabilityValidator,
                                        RepoConfigManager repoConfigManager,
                                        MiroMergeCheck miroMergeCheck,
                                        MergeOperations mergeOperations)
        {
            this.commentCreator = commentCreator;
            this.mergeRequestRepository = mergeRequestRepository;
            this.mergeabilityValidator = mergeabilityValidator;
            this.repoConfigManager = repoConfigManager;
            this.miroMergeCheck = miroMergeCheck;
            this.mergeOperations = mergeOperations;
        }

        public async Task<WebhookResponse> Handle(IssueCommentEvent issueCommentEvent)
        {
            var isCreatedAction = issueCommentEvent.Action.Equals("created", StringComparison.OrdinalIgnoreCase);
            if (!isCreatedAction)
            {
                logger.WithExtraData(new { issueEvent = issueCommentEvent.Action }).Information("Miro only handles new comments");
                return new WebhookResponse(false, "Miro only handles new comments");
            }

            var comment = issueCommentEvent.Comment.Body.Trim();

            var regex = new Regex($"^.*({CommentsConsts.MergeCommand}|{CommentsConsts.CancelCommand}|{CommentsConsts.InfoCommand}|{CommentsConsts.WipCommand}).*$", RegexOptions.IgnoreCase);
            var match = regex.Match(comment);

            if (!match.Success)
            {
                logger.WithExtraData(new { comment }).Information("Comment doesn't contain a miro command, ignoring");
                return new WebhookResponse(false, "Comment doesn't contain a miro command, ignoring");
            }
            
            return await HandleMiroCommand(issueCommentEvent, match.Groups[1].Value.ToLower());
        }

        private async Task<WebhookResponse> HandleMiroCommand(IssueCommentEvent issueCommentEvent, string miroCommand)
        {
            var owner = issueCommentEvent.Repository.Owner.Login;
            var repo = issueCommentEvent.Repository.Name;
            var prId = issueCommentEvent.Issue.Number;
            
            logger.WithExtraData(new { miroCommand, owner, repo, prId }).Information($"Handling miro comment command");

            var mergeRequest = await mergeRequestRepository.Get(owner, repo, prId);

            if (mergeRequest == null)
            {
                logger.WithExtraData(new { miroCommand, owner, repo, prId }).Warning($"Received miro command on unknown PR, Miro can't handle this");
                return new WebhookResponse(false, "Received miro command on unknown PR, Miro can't handle this");
            }


            switch (miroCommand)
            {
                case CommentsConsts.CancelCommand:
                    return await HandleMiroCancelCommand(owner, repo, prId);
                case CommentsConsts.MergeCommand:
                    return await HandleMiroMergeCommand(owner, repo, prId);
                case CommentsConsts.InfoCommand:
                    return await PrintMergeInfo(mergeRequest);
                case CommentsConsts.WipCommand:
                    return await HandleMiroWipCommand(owner, repo, prId);
                default:
                    logger.WithExtraData(new { miroCommand }).Error("Comment was supposed to contain a miro command but did not");
                    return new WebhookResponse(false, "Comment doesn't contain a miro command, ignoring");

            }
        }

        private async Task<WebhookResponse> HandleMiroWipCommand(string owner, string repo, int prId)
        {
             await Task.WhenAll(
                mergeRequestRepository.UpdateMergeCommand(owner, repo, prId, false, DateTime.MaxValue), 
                commentCreator.CreateComment(owner, repo, prId, CommentsConsts.MiroWipHeader, CommentsConsts.MiroWipBody));
            return new WebhookResponse(true, "handled Miro wip command");
        }

        private async Task<WebhookResponse> HandleMiroCancelCommand(string owner, string repo, int prId)
        {
            await Task.WhenAll(
                mergeRequestRepository.UpdateMergeCommand(owner, repo, prId, false, DateTime.MaxValue), 
                commentCreator.CreateComment(owner, repo, prId, CommentsConsts.MiroCancelHeader, CommentsConsts.MiroCancelBody));
            return new WebhookResponse(true, "handled Miro cancel command");
        }

        private async Task<WebhookResponse> HandleMiroMergeCommand(string owner, string repo, int prId)
        {
            var mergeRequest = await mergeRequestRepository.UpdateMergeCommand(owner, repo, prId, true, DateTime.UtcNow);

            var config = await repoConfigManager.GetConfig(owner, repo);
            if (config.IsWhitelistStrict())
            {
                logger.WithMergeRequestData(mergeRequest).Information("Repository has a whitelist-strict merge policy, resolving miro check on PR");
                await miroMergeCheck.ResolveMiroMergeCheck(mergeRequest);
            }
            var merged = await mergeOperations.TryToMerge(mergeRequest);
            return new WebhookResponse(true, $"handled Miro merge command, did branch merge: {merged}");
        }

        private async Task<WebhookResponse> PrintMergeInfo(MergeRequest mergeRequest)
        {
            if (mergeRequest == null)
            {
                logger.Warning($"Received TryToMerge command from a null mergeRequest");
                throw new Exception("Can not merge, PR is not defined");
            }

            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;
            var mergeabilityValidationErrors = await mergeabilityValidator.ValidateMergeability(mergeRequest);

            if (mergeabilityValidationErrors.Any())
            {
                var errors = mergeabilityValidationErrors.Select(x => x.Error);
                await commentCreator.CreateListedComment(owner, repo, prId, CommentsConsts.MiroInfoMergeNotReady, errors.ToList());
                return new WebhookResponse(true, $"handled Miro info command");;
            }
            await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.MiroInfoMergeReady, CommentsConsts.PrIsMergeableBody);
            return new WebhookResponse(true, $"handled Miro info command");
        }
    }
}