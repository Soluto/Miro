using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Miro.Models.Github.RequestPayloads;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Newtonsoft.Json;
using Serilog;

namespace Miro.Services.Github
{
    public class PrMerger
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly ILogger logger = Log.ForContext<PrMerger>();

        public PrMerger(GithubHttpClient githubHttpClient)
        {
            this.githubHttpClient = githubHttpClient;
        }

        public async Task Merge(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;
            var title = mergeRequest.Title;

            var payload = new MergePrPayload()
            {
                CommitMessage = $"Merging PR #{prId} - {title}",
                CommitTitle = $"Merging PR #{prId} - {title}",
                MergeMethod = "squash",
            };

            string uri = $"/repos/{owner}/{repo}/pulls/{prId}/merge";
            logger.WithMergeRequestData(mergeRequest).Information($"Making github merge request");
            var response = await githubHttpClient.Put(uri, payload);

            MergePrResponse mergeResponse = new MergePrResponse();
            try
            {
                var contentAsString = await response.Content.ReadAsStringAsync();
                mergeResponse = JsonConvert.DeserializeObject<MergePrResponse>(contentAsString);
            }
            catch (Exception)
            {
            }

            if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                throw new PullRequestMismatchException(mergeResponse.Message);
            }
            if (response.StatusCode != HttpStatusCode.OK || mergeResponse == null || !mergeResponse.Merged)
            {
                var msg = mergeResponse != null ? mergeResponse.Message : "unknown";
                var extraLogData = new {owner, repo, prId, uri, msg, responseStatus = response.StatusCode};
                logger.WithExtraData(extraLogData).Error($"Failed Making github merge request");
                throw new Exception(msg);
            }
        }
    }
}