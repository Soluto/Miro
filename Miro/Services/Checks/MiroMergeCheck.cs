using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Miro.Models.Checks;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Comments;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Serilog;

namespace Miro.Services.Checks
{
    public class MiroMergeCheck
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly PrStatusChecks prStatusCheckUpdater;
        private readonly ILogger logger = Log.ForContext<MiroMergeCheck>();

        public MiroMergeCheck(
            GithubHttpClient githubHttpClient,
            PrStatusChecks prStatusCheckUpdater)
        {
            this.githubHttpClient = githubHttpClient;
            this.prStatusCheckUpdater = prStatusCheckUpdater;
        }

        public async Task AddMiroMergeRequiredCheck(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var sha = mergeRequest.Sha;
            
            logger.WithMergeRequestData(mergeRequest).Information($"Creating pending Miro Merge check to a branch");
            
            try
            {
                await prStatusCheckUpdater.UpdateStatusCheck(owner, repo, sha, CommentsConsts.MiroMergeCheckName, "pending");
            }
            catch (Exception e)
            {
                logger.WithMergeRequestData(mergeRequest).Error(e, $"Could not resolve Miro Merge check to a branch");
            }
        }

         public async Task ResolveMiroMergeCheck(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var sha = mergeRequest.Sha;
            var branch = mergeRequest.Branch;
            
            logger.WithMergeRequestData(mergeRequest).Information($"Resolve Miro Merge check to a branch");
            
            try
            {
                await prStatusCheckUpdater.UpdateStatusCheck(owner, repo, sha, CommentsConsts.MiroMergeCheckName);
            }
            catch (Exception e)
            {
                logger.WithMergeRequestData(mergeRequest).Error(e, $"Could not resolve Miro Merge check on sha, retrying with branch");
                try
                {
                    await prStatusCheckUpdater.UpdateStatusCheck(owner, repo, branch, CommentsConsts.MiroMergeCheckName);
                }
                catch (Exception er)
                {
                   logger.WithMergeRequestData(mergeRequest).Error(er, $"Could not resolve Miro Merge check on branch");
                }
            }
        }
    }
}