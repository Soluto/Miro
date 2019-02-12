using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Miro.Models.Github.RequestPayloads;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Comments;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Newtonsoft.Json;
using Serilog;

namespace Miro.Services.Github
{
    public class PrStatusChecks
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly ILogger logger = Log.ForContext<PrStatusChecks>();

        public PrStatusChecks(GithubHttpClient githubHttpClient,
        MergeRequestsRepository mergeRequestsRepository)
        {
            this.githubHttpClient = githubHttpClient;
        }
        public async Task UpdateStatusCheck(string owner, string repo, string shaOrBranch, string statusCheck, string state = "success")
        {
            var payload = new UpdateStatusCheckPayload()
            {
                State = state,
                Context = statusCheck,
                Description = CommentsConsts.MiroMergeCheckDescription,
            };
            var uri = $"/repos/{owner}/{repo}/statuses/{shaOrBranch}";
            logger.WithExtraData(new { owner, repo, shaOrBranch, uri, payload }).Information($"Making github update status check request");

            var response = await githubHttpClient.Post(uri, payload);
            response.EnsureSuccessStatusCode();
        }
          public async Task<List<PullRequestCheckStatus>> GetStatusChecks(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var sha = mergeRequest.Sha;

            var uri = $"/repos/{owner}/{repo}/statuses/{sha}";
            logger.WithMergeRequestData(mergeRequest).Information($"Making github get status checks for PR request");

            return await githubHttpClient.Get<List<PullRequestCheckStatus>>(uri);
        }
    }
}