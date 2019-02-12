using System;
using System.Net.Http;
using System.Threading.Tasks;
using Miro.Models.Github.RequestPayloads;
using Miro.Models.Github.Responses;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Newtonsoft.Json;
using Serilog;

namespace Miro.Services.Github
{
    public class PrDeleter
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly ILogger logger = Log.ForContext<PrDeleter>();
        public PrDeleter(GithubHttpClient githubHttpClient)
        {
            this.githubHttpClient = githubHttpClient;
        }
          public async Task DeleteBranch(string owner, string repo, string branch)
        {
            if (branch == null) 
            {
                throw new Exception($"Could not update branch for {owner}/{repo}, branch unknown");
            }
    
            string uri = $"/repos/{owner}/{repo}/git/refs/heads/{branch}";
            logger.WithExtraData(new {owner, repo, branch}).Information($"Making github delete branch request");

            var response = await githubHttpClient.Delete(uri);

            try {
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e)
            {
                string reason = null;
                try
                {
                    var contentAsString = await response.Content.ReadAsStringAsync();
                    reason = JsonConvert.DeserializeObject<dynamic>(contentAsString);
                }
                catch (System.Exception){}
                logger.WithExtraData(new {reason, owner, repo, branch}).Error(e, $"Could not delete branch");
            }

        }
    }
}