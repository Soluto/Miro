using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Models.MiroConfig;
using Miro.Services.Checks;
using Miro.Services.Comments;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Miro.Services.MiroConfig;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;

namespace Miro.Services.Github.EventHandlers
{
    public class PullRequestEventHandler : IWebhookEventHandler<PullRequestEvent>
    {
        private readonly PrDeleter prDeleter;
        private readonly MergeRequestsRepository mergeRequestRepository;
        private readonly PrStatusChecks prStatusChecks;
        private readonly ChecksManager checksManager;
        private readonly RepoConfigManager repoConfigManager;
        private readonly MiroMergeCheck miroMergeCheck;
        private readonly MergeOperations mergeOperations;
        private readonly CommentCreator commentCreator;
        private readonly ILogger logger = Log.ForContext<PullRequestEventHandler>();

        public PullRequestEventHandler(
            PrDeleter prDeleter,
            MergeRequestsRepository mergeRequestRepository,
            PrStatusChecks prStatusChecks,
            ChecksManager checksManager,
            RepoConfigManager repoConfigManager,
            MiroMergeCheck miroMergeCheck,
            CommentCreator commentCreator,
            MergeOperations mergeOperations)
        {
            this.prDeleter = prDeleter;
            this.mergeRequestRepository = mergeRequestRepository;
            this.prStatusChecks = prStatusChecks;
            this.checksManager = checksManager;
            this.repoConfigManager = repoConfigManager;
            this.miroMergeCheck = miroMergeCheck;
            this.commentCreator = commentCreator;
            this.mergeOperations = mergeOperations;
        }

        public async Task<WebhookResponse> Handle(PullRequestEvent payload)
        {
            var payloadAction = payload.Action;

            if (payloadAction.EndsWith("opened", StringComparison.OrdinalIgnoreCase))
            {
               return await HandleOpenedEvent(payload);
            }
            else if (payloadAction.Equals("closed", StringComparison.OrdinalIgnoreCase))
            {
               return await HandleClosedEvent(payload);
            }
            else if (payloadAction.Equals("synchronize", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleSynchronizeEvent(payload);
            }
            return new WebhookResponse(false, $"PR {payloadAction} event, Ignoring");
        }

        private async Task<WebhookResponse> HandleSynchronizeEvent(PullRequestEvent payload)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var prId = payload.Number;
            var sha = payload.PullRequest.Head.Sha;
            var extraLoggerData = new { owner, repo, prId, sha };
            

            var mergeRequest = await mergeRequestRepository.Get(owner, repo, prId);
            
            if (mergeRequest == null)
            {
                logger.WithExtraData(extraLoggerData).Information("Synchronize event on unknown Pull Request, ignore");
                return new WebhookResponse(false, "Synchronize event on unknown Pull Request, ignore");
            }

            var config = await repoConfigManager.GetConfig(owner, repo);
            logger.WithMergeRequestData(mergeRequest).Information("Handling synchronize event, updating sha");
            
            var updatedMergeRequest = await mergeRequestRepository.UpdateShaAndClearStatusChecks(mergeRequest.Owner, mergeRequest.Repo, mergeRequest.PrId, sha);

            if (config.IsWhitelistStrict() && updatedMergeRequest.ReceivedMergeCommand)
            {
                logger.WithMergeRequestData(updatedMergeRequest).Information("Repository has a whitelist-strict merge policy, resolving miro check on PR");
                await miroMergeCheck.ResolveMiroMergeCheck(updatedMergeRequest);
            }
            
            return new WebhookResponse(true, "Handling synchronize event, sha updated");
        }

        private async Task<WebhookResponse> HandleClosedEvent(PullRequestEvent payload)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var prId = payload.Number;
            var extraLoggerData = new { owner, repo, prId };

             logger.WithExtraData(extraLoggerData).Information("Handling PR closed event for PR - Deleting");
                var deletedMergeRequest = await mergeRequestRepository.Delete(owner, repo, prId);

                if (deletedMergeRequest != null && payload.PullRequest.Merged)
            {
                logger.WithExtraData(extraLoggerData).Information($"PR was merged, deleting branch");
                var wasBranchDeleted = await DeleteBranchByStrategy(deletedMergeRequest);
                return new WebhookResponse(true, $"PR merged event, was branch deleted: {wasBranchDeleted}");
            }
            return new WebhookResponse(true, "PR closed event");
        }

        private async Task<bool> DeleteBranchByStrategy(MergeRequest mergeRequestToDelete)
        {
            var config = await repoConfigManager.GetConfig(mergeRequestToDelete.Owner, mergeRequestToDelete.Repo);
            if (config.DeleteAfterMerge)
            {
                await prDeleter.DeleteBranch(mergeRequestToDelete.Owner, mergeRequestToDelete.Repo, mergeRequestToDelete.Branch);
                return true;
            }
            return false;
        }

        private async Task<WebhookResponse> HandleOpenedEvent(PullRequestEvent payload)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var prId = payload.Number;
            var title = payload.PullRequest.Title;
            var sha = payload.PullRequest.Head.Sha;
            var branch = payload.PullRequest.Head.Ref;
            var author = payload.PullRequest.User.Login;
            var baseRef = payload.PullRequest.Base.Ref;
            var payloadAction = payload.Action;
            var isFork = payload.PullRequest.Head.Repo?.Fork ?? false;
            var config = await repoConfigManager.GetConfig(owner, repo);
            var mergePolicy = config.MergePolicy;
            var defaultBranch = config.DefaultBranch;
            var extraLoggerData = new { owner, repo, prId, title, sha, branch, author, baseRef, payloadAction, mergePolicy, isFork, defaultBranch };
            if (baseRef != defaultBranch)
            {
                 logger.WithExtraData(extraLoggerData)
                .Information("Open PR Event was not targeted at default branch, ignoring");
                 return new WebhookResponse(false, $"Open PR Event was targeted {baseRef}, Miro only handles {defaultBranch} branch");
            }

             logger.WithExtraData(extraLoggerData)
                .Information($"Handling PR opened event - Creating a PENDING merge request");

                var mergeRequest = new MergeRequest
                {
                    Id = new ObjectId(),
                    CreatedAt = DateTime.UtcNow,
                    Owner = owner,
                    Repo = repo,
                    PrId = prId,
                    Author = author,
                    Title = title,
                    Sha = sha,
                    IsFork = isFork,
                    Branch = payload.PullRequest.Head.Ref,
                    ReceivedMergeCommandTimestamp = DateTime.MaxValue
                };

                await mergeRequestRepository.Create(mergeRequest);

                if (config.IsBlacklist() && !mergeRequest.Title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase))
                {
                    logger.WithMergeRequestData(mergeRequest).Information("New Pull Request has blacklist configuration");
                    await mergeRequestRepository.UpdateMergeCommand(owner, repo, prId, true, DateTime.UtcNow);
                    await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.BlackListPullRequestHeader, CommentsConsts.BlackListPullRequestBody);
                } 
                else if (config.IsBlacklist() && mergeRequest.Title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase)) {
                    logger.WithMergeRequestData(mergeRequest).Information("New Pull Request has blacklist configuration but titled as WIP");
                    await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.BlackListPullRequestWipHeader, CommentsConsts.BlackListPullRequestWipBody);
                }
                else if (config.IsWhitelistStrict())
                {
                    logger.WithMergeRequestData(mergeRequest).Information("Opened PR has a whitelist-strict merge policy");
                    await miroMergeCheck.AddMiroMergeRequiredCheck(mergeRequest);
                }
                
                if (payloadAction == "reopened")
                {
                   await HandleReopenedPullRequest(mergeRequest); 
                }
                return new WebhookResponse(true, $"Handling PR {payloadAction} event - Creating a PENDING merge request");
        }

        private async Task HandleReopenedPullRequest(MergeRequest mergeRequest)
        {
            logger.WithMergeRequestData(mergeRequest).Information("Handling PR reopened event for PR - Checking if it has passed all tests");
            var checks = await prStatusChecks.GetStatusChecks(mergeRequest);

            if (checks == null || !checks.Any())
            {
                 logger.WithMergeRequestData(mergeRequest).Information("PR Reopen event had no past passing tests");
                 return;
            }
            var onlyRequiredChecks = await checksManager.FilterNonRequiredChecks(mergeRequest.Owner, mergeRequest.Repo, checks);
            
            logger
            .WithMergeRequestData(mergeRequest)
            .WithExtraData(new {oldTests = string.Join(",", checks.Select(x => x.Context)), filteredTests = string.Join(",", onlyRequiredChecks.Select(x => x.Context))})
            .Information("Adding old tests to reopened PullRequest");

            await mergeRequestRepository.UpdateCheckStatus(mergeRequest.Owner, mergeRequest.Repo, mergeRequest.PrId, onlyRequiredChecks);
        }
    }
}