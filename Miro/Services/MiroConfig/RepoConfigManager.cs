using System;
using System.Threading.Tasks;
using Miro.Models.Github.Entities;
using Miro.Models.MiroConfig;
using Miro.Services.Github;
using Miro.Services.Logger;
using MiroConfig;
using Serilog;

namespace Miro.Services.MiroConfig
{
    public class RepoConfigManager
    {
        private readonly ILogger logger = Log.ForContext<RepoConfigManager>();
        private readonly FileRetriever fileRetriever;
        private readonly RepoConfigRepository repoConfigRepository;
        private readonly string REPO_CONFIG_FILE_NAME = ".miro.yml";

        public RepoConfigManager(
            FileRetriever fileRetriever,
            RepoConfigRepository repoConfigRepository
            )
        {
            this.fileRetriever = fileRetriever;
            this.repoConfigRepository = repoConfigRepository;
        }

        public async Task<RepoConfig> UpdateConfig(string owner, string repo)
        {
            var content = await FetchConfigFromGithub(owner, repo);
            logger.WithExtraData(new {content}).Information("Updating Repo with new Repo Config");    
            await repoConfigRepository.Update(content);
            return content;
        }

         public async Task<RepoConfig> GetConfig(string owner, string repo)
        {
            var result = await repoConfigRepository.Get(owner, repo);

            if (result == null)
            {
                result = await FetchConfigFromGithub(owner, repo);
                await repoConfigRepository.Create(result);
            }
            return result;

        }

        private async Task<RepoConfig> FetchConfigFromGithub(string owner, string repo)
        {
            var file = await fileRetriever.GetFile(owner, repo, REPO_CONFIG_FILE_NAME);

            if (file == null)
            {
                logger.WithExtraData(new {owner, repo}).Warning("No config file found for repo");   
                return new RepoConfig
                {
                    Repo = repo,
                    Owner = owner
                };
            }
            var decoded = file.DecodeContent<RepoConfig>();

            decoded.Owner = owner;
            decoded.Repo = repo;
            decoded.UpdatedAt = DateTime.UtcNow;

            if (!decoded.IsValidMergePolicy())
            {
                decoded.MergePolicy = "whitelist";
                logger.WithExtraData(new {decoded}).Warning("Invalid miro yml configuration given for merge policy");    
            }
            if (!decoded.IsValidUpdateBranchStrategy())
            {
                decoded.UpdateBranchStrategy = "oldest";
                logger.WithExtraData(new {decoded}).Warning("Invalid miro yml configuration given for update branch strategy");   
            }
            
            return decoded;
        }
    }
}