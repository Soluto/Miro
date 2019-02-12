using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Miro.Models.Checks;
using Miro.Models.Github.RequestPayloads;
using Miro.Models.Github.Responses;
using Miro.Models.Merge;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Serilog;

namespace Miro.Services.Checks
{
    public class ChecksManager
    {
        private readonly MergeRequestsRepository mergeRequestsRepository;
        private readonly ChecksRepository checksRepository;
        private readonly ChecksRetriever checksRetriever;
        private readonly ILogger logger = Log.ForContext<ChecksManager>();


        public ChecksManager(
            MergeRequestsRepository mergeRequestsRepository,
            ChecksRepository checksRepository,
            ChecksRetriever checksRetriever)
        {
            this.mergeRequestsRepository = mergeRequestsRepository;
            this.checksRepository = checksRepository;
            this.checksRetriever = checksRetriever;
        }

          public async Task<List<string>> UpdateChecks(string owner, string repo)
        {
            var checks = await checksRetriever.GetRequiredChecks(owner, repo);
            
            if (checks == null)
                return new List<string>();

            logger.WithExtraData(new {owner, repo, checks = string.Join(",", checks)}).Information($"Receieved a call to Update checks");
            await checksRepository.Update(owner, repo, checks);
            return checks;
        }

        public async Task<List<string>> GetMissingChecks(string owner, string repo, List<CheckStatus> requestChecks)
        {
            var checksFromRepo = await GetRequiredChecks(owner, repo);
            return CompareChecks(requestChecks, checksFromRepo);
        }

        public async Task<bool> IsRequiredCheck(string owner, string repo, string checkName)
        {
            var checks = await GetRequiredChecks(owner, repo);
            return checks.Any(x => x == checkName);
        }

        public async Task<List<PullRequestCheckStatus>> FilterNonRequiredChecks(string owner, string repo, List<PullRequestCheckStatus> requestChecks)
        {
            var checks = await GetRequiredChecks(owner, repo);
            return requestChecks.Where(reqCheck => checks.Contains(reqCheck.Context)).ToList();
        }


        private static List<string> CompareChecks(List<CheckStatus> requestChecks, List<string> checks)
        {
            var finalChecks = new List<string>();
            if (checks != null) finalChecks.AddRange(checks);
            return finalChecks.Where(check => !requestChecks.Any(x => x.Name == check && x.Status == "success")).ToList();
        }

        private async Task<List<string>> GetRequiredChecks(string owner, string repo)
        {
            var checksFromDb = await checksRepository.Get(owner, repo);

            if (checksFromDb != null)
            {
                return checksFromDb.CheckNames;
            }
            logger.WithExtraData(new { owner, repo }).Information($"No checks found in DB, fetching file from repository");
            var checksFromGithub = await checksRetriever.GetRequiredChecks(owner, repo);

            if (checksFromGithub == null)
            {
                 logger.WithExtraData(new { owner, repo }).Error($"No required checks found in repo");
                 return new List<string>();
            }

            await checksRepository.Update(owner, repo, checksFromGithub);
            return checksFromGithub;
        }
    }
}