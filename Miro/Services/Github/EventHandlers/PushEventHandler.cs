using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Models.MiroConfig;
using Miro.Services.Checks;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Miro.Services.MiroConfig;
using Serilog;

namespace Miro.Services.Github.EventHandlers
{
    public class PushEventHandler : IWebhookEventHandler<PushEvent>
    {
        private readonly ILogger logger = Log.ForContext<PushEventHandler>();

        private readonly MergeRequestsRepository mergeRequestRepository;
        private readonly MiroMergeCheck miroMergeCheck;
        private readonly RepoConfigManager repoConfigManager;
        private readonly ChecksManager checksManager;
        private readonly PrUpdater prUpdater;

        public PushEventHandler(
            MergeRequestsRepository mergeRequestRepository,
            MiroMergeCheck miroMergeCheck,
            RepoConfigManager RepoConfigManager,
            ChecksManager checksManager,
            PrUpdater prUpdater)
        {
            this.mergeRequestRepository = mergeRequestRepository;
            this.miroMergeCheck = miroMergeCheck;
            repoConfigManager = RepoConfigManager;
            this.checksManager = checksManager;
            this.prUpdater = prUpdater;
        }

        public async Task<WebhookResponse> Handle(PushEvent payload)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var sha = payload.After;
            var branch = payload.Ref;
            var config = await repoConfigManager.GetConfig(owner, repo);
            var defaultBranch = config.DefaultBranch;

            if (payload.Ref.Contains(defaultBranch, StringComparison.OrdinalIgnoreCase))
            {
                var updatedConfig = await repoConfigManager.UpdateConfig(owner, repo);
                var updated = await UpdateNextPrByStrategy(payload, updatedConfig);
                await checksManager.UpdateChecks(owner, repo);
                return new WebhookResponse(true, $"Handled Push on default branch, was next branch updated: {updated}");
            }

            logger.WithExtraData(new {owner = owner, repo = repo, branch = branch}).Information($"Received push event on branch, Handling..");
            var extraLogData = new {owner, repo, branch, sha};

            if (!branch.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
            {
                logger.WithExtraData(extraLogData).Information($"Push on branch, is not a head commit, ignoring...");
                return new WebhookResponse(false, $"Received Push on branch which is not head, ignored");
            }
            return await HandlePushOnPr(payload, config);
        }

        private async Task<WebhookResponse> HandlePushOnPr(PushEvent payload, RepoConfig config)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;
            var sha = payload.After;
            var branch = payload.Ref;
            var extraLogData = new {owner, repo, branch, sha};

            var strippedBranchName = branch.Substring("refs/heads/".Length);
            var mergeRequest = await mergeRequestRepository.GetByBranchName(owner, repo, strippedBranchName);
            if (mergeRequest == null)
            {
                logger.WithExtraData(extraLogData).Information("Push on branch, does not exist in Miro DB, ignoring");
                return new WebhookResponse(false, "Push on branch, does not exist in Miro DB, ignoring");
            }

            logger.WithMergeRequestData(mergeRequest).Information($"Push on branch found in DB, Clearing status checks and updating sha");

            var updatedMergeRequest = await mergeRequestRepository.UpdateShaAndClearStatusChecks(mergeRequest.Owner, mergeRequest.Repo, mergeRequest.PrId, sha);

            if (config.IsWhitelistStrict() && updatedMergeRequest.ReceivedMergeCommand)
            {
                logger.WithMergeRequestData(updatedMergeRequest).Information("Repository has a whitelist-strict merge policy, resolving miro check on PR");
                await miroMergeCheck.ResolveMiroMergeCheck(updatedMergeRequest);
            }

            return new WebhookResponse(true, $"Push on branch is a known PR, updated sha to {sha} and cleared status checks");
        }

        private async Task<bool> UpdateNextPrByStrategy(PushEvent payload, RepoConfig repoConfig)
        {
            var owner = payload.Repository.Owner.Login;
            var repo = payload.Repository.Name;    
            logger.WithExtraData(new {owner, repo, strategy = repoConfig.UpdateBranchStrategy}).Information("Updating next PR by strategy");

            List<MergeRequest> prsToUpdate = null;     

            switch (repoConfig.UpdateBranchStrategy)
            {
                case "oldest":
                    var singlePr = await mergeRequestRepository.GetOldestPr(owner, repo);
                    if (singlePr != null) prsToUpdate = new List<MergeRequest> {singlePr};
                break;

                case "all":
                    var allPrs = await mergeRequestRepository.Get(owner, repo);
                    if (allPrs != null && allPrs.Any()) prsToUpdate = allPrs.Where(x => x.ReceivedMergeCommand).ToList();
                break;

                case "none":
                default:
                break;
            }
            if (prsToUpdate == null || !prsToUpdate.Any())
            {
                logger.WithExtraData(new { owner, repo }).Warning($"Could not find next PRs to update based on after PR was merged");
                return false;
            }
           var tasks = prsToUpdate.Select(pr => UpdateSinglePr(pr, repoConfig));   
           var completions = await Task.WhenAll(tasks);
           
           return completions.Any(x => x);
        }

          private async Task<bool> UpdateSinglePr(MergeRequest pullRequest, RepoConfig config)
        {
            var branch = pullRequest.Branch;
            var prId = pullRequest.PrId;
            logger.WithMergeRequestData(pullRequest).Information($"updating branch on next PullRequest");
            
            try
            {
                await prUpdater.UpdateBranch(pullRequest.Owner, pullRequest.Repo, branch);
                return true;
            }
            catch (Exception e)
            {
                logger.WithMergeRequestData(pullRequest).Warning(e, "Unable to update branch on next PR");
                return false;
            } 
        }

      
    }
}