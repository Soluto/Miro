using System;
using System.Threading.Tasks;
using Miro.Models.Github.Entities;
using Miro.Services.Logger;
using Serilog;

namespace Miro.Services.Github
{
    public class FileRetriever
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly ILogger logger = Log.ForContext<FileRetriever>();


        public FileRetriever(GithubHttpClient githubHttpClient)
        {
            this.githubHttpClient = githubHttpClient;
        }
                
        public async Task<FileContent> GetFile(string owner, string repo, string fileName)
        {
            logger.WithExtraData(new {owner, repo, fileName}).Information("Fetching file from repo");
            try
            {
                var payload = await githubHttpClient.Get<FileContent>($"/repos/{owner}/{repo}/contents/{fileName}");
                if (payload == null) 
                {
                    logger.WithExtraData(new {owner, repo, fileName}).Information("File not found");
                    return null;
                }
                logger.WithExtraData(new {owner, repo, fileName, name = payload.Name, sha = payload.Sha, content = payload.Content}).Information("File found from repo");
                return payload;
            }
            catch (Exception e)
            {
               logger.WithExtraData(new {owner, repo, fileName}).Information("File not found");
               return null;
            }
        }
    }
}