using System.Linq;
using System.Threading.Tasks;
using Miro.Models.Checks;
using Miro.Models.Merge;
using MongoDB.Driver;
using System.Collections.Generic;
using System;
using Miro.Models.Github.RequestPayloads;

namespace Miro.Services.Merge
{
    public class MergeRequestsRepository
    {
        private readonly IMongoCollection<MergeRequest> collection;

        public MergeRequestsRepository(IMongoCollection<MergeRequest> collection)
        {
            this.collection = collection;
        }

        public async Task<List<MergeRequest>> Get() => await collection.Find(_ => true).ToListAsync();

        public async Task<MergeRequest> Get(string owner, string repo, int prId)
        {
            return await collection.Find(r => r.Owner == owner && r.Repo == repo && r.PrId == prId).FirstOrDefaultAsync();
        }

        public async Task<MergeRequest> GetOldestPr(string owner, string repo)
        {
            var sortDefinition = Builders<MergeRequest>.Sort.Ascending("ReceivedMergeCommandTimestamp");

            var allPrs = await collection.Find(r => r.Owner == owner && r.Repo == repo && r.ReceivedMergeCommand && r.State != "MERGED").Sort(sortDefinition).ToListAsync();

            // First Attempt - First PR with no failing tests and merge command
            var allPrsWithoutFailingChecks = allPrs.FirstOrDefault(pr => pr.NoFailingChecks());

            // Second Attempt - First PR merge command
            return allPrsWithoutFailingChecks ?? allPrs.FirstOrDefault();
        }

        public async Task<List<MergeRequest>> Get(string owner, string repo)
        {
            return (await collection.FindAsync(r => r.Owner == owner && r.Repo == repo)).ToList();
        }

        public async Task<MergeRequest> GetByBranchName(string owner, string repo, string branch)
        {
            return await collection.Find(r => r.Owner == owner && r.Repo == repo && r.Branch == branch).FirstOrDefaultAsync();
        }

        public async Task<MergeRequest> GetBySha(string owner, string repo, string sha)
        {
            return await collection.Find(r => r.Owner == owner && r.Repo == repo && r.Sha == sha).FirstOrDefaultAsync();
        }

        public async Task<MergeRequest> UpdateMergeCommand(string owner, string repo, int prId, bool mergeCommand, DateTime mergeCommandTime)
        {
            var options = new FindOneAndUpdateOptions<MergeRequest>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };
            var update = Builders<MergeRequest>.Update
                .Set(r => r.ReceivedMergeCommand, mergeCommand)
                .Set(r => r.ReceivedMergeCommandTimestamp, mergeCommandTime);

            return await collection.FindOneAndUpdateAsync<MergeRequest>(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId, update, options);
        }


        public async Task<MergeRequest> UpdateCheckStatus(string owner, string repo, int prId, List<PullRequestCheckStatus> checkList)
        {
            var updateTime = DateTime.UtcNow;

            var options = new FindOneAndUpdateOptions<MergeRequest>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };
            var mergeRequest = await Get(owner, repo, prId);

            checkList.ForEach(requestCheck =>
            {
                var check = mergeRequest.Checks.FirstOrDefault(mergeRequestCheck => mergeRequestCheck.Name == requestCheck.Context);
                if (check != null)
                {
                    check.Status = requestCheck.State;
                    check.UpdatedAt = updateTime;
                    check.TargetUrl = requestCheck.TargetUrl;
                }
                else
                {
                    mergeRequest.Checks.Add(new CheckStatus
                    {
                        Name = requestCheck.Context,
                        Status = requestCheck.State,
                        UpdatedAt = updateTime,
                        TargetUrl = requestCheck.TargetUrl
                    });
                }
            });

            var update = Builders<MergeRequest>.Update.Set(r => r.Checks, mergeRequest.Checks);

            return await collection.FindOneAndUpdateAsync<MergeRequest>(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId, update, options);
        }


        public async Task<MergeRequest> UpdateCheckStatus(string owner, string repo, int prId, string checkName, string checkStatus, string targetUrl)
        {
            MergeRequest response;
            var updateTime = DateTime.UtcNow;

             var options = new FindOneAndUpdateOptions<MergeRequest>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };

            var filterForCheck = Builders<MergeRequest>.Filter.Where(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId &&
                                                 r.Checks.Any(i => i.Name == checkName));
            var updateForExisting = Builders<MergeRequest>.Update
                    .Set(x => x.Checks[-1].Status, checkStatus)
                    .Set(x => x.Checks[-1].UpdatedAt, updateTime)
                    .Set(x => x.Checks[-1].TargetUrl, targetUrl);                                     

            response = await collection.FindOneAndUpdateAsync(filterForCheck, updateForExisting, options);

            if (response == null)
            {
                var filterForItem = Builders<MergeRequest>.Filter.Where(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId);
                var updateForNew = Builders<MergeRequest>.Update
                    .Push(x => x.Checks, new CheckStatus
                {
                    Name = checkName,
                    Status = checkStatus,
                    UpdatedAt = updateTime,
                    TargetUrl = targetUrl
                });

                response = await collection.FindOneAndUpdateAsync<MergeRequest>(filterForItem, updateForNew, options);
            }
            return response;
        }

        public async Task UpdateState(string owner, string repo, int prId, string state)
        {
            var options = new FindOneAndUpdateOptions<MergeRequest>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };
            var update = Builders<MergeRequest>.Update.Set(r => r.State, state);

            await collection.FindOneAndUpdateAsync<MergeRequest>(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId, update, options);
        }

        public async Task<MergeRequest> UpdateShaAndClearStatusChecks(string owner, string repo, int prId, string sha)
        {
            var options = new FindOneAndUpdateOptions<MergeRequest>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };
            var update = Builders<MergeRequest>.Update.Set(r => r.Sha, sha).Set(r => r.Checks, new List<CheckStatus>());

            return await collection.FindOneAndUpdateAsync<MergeRequest>(r => r.Owner == owner &&
                                                 r.Repo == repo &&
                                                 r.PrId == prId, update, options);
        }

        public async Task Create(MergeRequest mergeRequest)
        {
            await collection.InsertOneAsync(mergeRequest);
        }

        public async Task<MergeRequest> Delete(string owner, string repo, int prId) => await collection.FindOneAndDeleteAsync(r => r.Owner == owner && r.Repo == repo && r.PrId == prId);
    }
}