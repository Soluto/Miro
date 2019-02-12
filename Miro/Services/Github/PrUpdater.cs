using System;
using System.Net.Http;
using System.Threading.Tasks;
using Miro.Models.Github.RequestPayloads;
using Miro.Models.Github.Responses;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Miro.Services.MiroConfig;
using Newtonsoft.Json;
using Serilog;

namespace Miro.Services.Github
{
    public class PrUpdater
    {
        private readonly GithubHttpClient githubHttpClient;
                
        private readonly RepoConfigManager repoConfigManager;

        private readonly ILogger logger = Log.ForContext<PrUpdater>();

        public PrUpdater(GithubHttpClient githubHttpClient, RepoConfigManager repoConfigManager)
        {
            this.repoConfigManager = repoConfigManager;
            this.githubHttpClient = githubHttpClient;
        }
          public async Task UpdateBranch(string owner, string repo, string branch)
        {
            var defaultBranch = (await repoConfigManager.GetConfig(owner, repo)).DefaultBranch;

            if (branch == null) 
            {
                throw new Exception($"Could not update branch for {owner}/{repo}, branch unknown");
            }
            if (defaultBranch == null) 
            {
                throw new Exception($"Could not update branch for {owner}/{repo}, head branch unknown");
            }
            
            var payload = new UpdateBranchPayload()
            {
                CommitMessage = $"Merge branch {defaultBranch} into {branch}",
                Base = branch,
                Head = defaultBranch,
            };
            string uri = $"/repos/{owner}/{repo}/merges";
            logger.WithExtraData(new {owner, repo, branch, defaultBranch, uri}).Information($"Making github update branch request");
            
            var response = await githubHttpClient.Post(uri, payload);

            try {
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e)
            {
                logger.WithExtraData(new {owner, repo, branch, uri}).Warning(e, $"Failed updating branch");
                throw e;
            }

        }
    }
}