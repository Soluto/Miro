using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Miro.Models.Checks;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Miro.Services.MiroConfig;
using Serilog;

namespace Miro.Services.Checks
{
    public class ChecksRetriever
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly RepoConfigManager repoConfigManager;
        private readonly ILogger logger = Log.ForContext<ChecksRetriever>();

        public ChecksRetriever(
            GithubHttpClient githubHttpClient,
            RepoConfigManager repoConfigManager)
        {
            this.githubHttpClient = githubHttpClient;
            this.repoConfigManager = repoConfigManager;
        }

        public async Task<List<string>> GetRequiredChecks(string owner, string repo)
        {
            var config = await repoConfigManager.GetConfig(owner, repo);
            var defaultBranch = config.DefaultBranch;

            var uri = $"/repos/{owner}/{repo}/branches/{defaultBranch}/protection/required_status_checks";
            logger.WithExtraData(new {owner, repo, defaultBranch, uri}).Information($"Retrieving required checks");
            
            var requiredChecks = await githubHttpClient.Get<RequiredChecksResponse>(uri);
            if (requiredChecks == null || !requiredChecks.Contexts.Any()) 
            {
                logger.WithExtraData(new {owner, repo}).Information($"No required checks found");
                return null;
            }
            logger.WithExtraData(new {owner, repo, checks = string.Join(",",requiredChecks.Contexts)}).Information($"Found required checks");
            return requiredChecks.Contexts.ToList();
        }
    }
}