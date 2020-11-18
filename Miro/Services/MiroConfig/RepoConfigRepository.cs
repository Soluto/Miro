using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Miro.Models.MiroConfig;
using MongoDB.Driver;

namespace MiroConfig
{
    public class RepoConfigRepository
    {
        private readonly IMongoCollection<RepoConfig> collection;

        public RepoConfigRepository(IMongoCollection<RepoConfig> collection)
        {
            this.collection = collection;
        }

         public Task<RepoConfig> Get(string owner, string repo)
        {
            return collection.Find(r => r.Owner == owner && r.Repo == repo).FirstOrDefaultAsync();
        }
         public Task<List<RepoConfig>> Get() => collection.Find(_ => true).ToListAsync();

         public Task Create(RepoConfig config)
        {
            return collection.InsertOneAsync(config);
        }

        public Task<RepoConfig> Update(RepoConfig config)
        {
            config.UpdatedAt = DateTime.UtcNow;
            var options = new FindOneAndUpdateOptions<RepoConfig>
            {
                IsUpsert = true
            };
             var update = Builders<RepoConfig>.Update
                .Set(r => r.UpdatedAt, DateTime.UtcNow)
                .Set(r => r.MergePolicy, config.MergePolicy)
                .Set(r => r.UpdateBranchStrategy, config.UpdateBranchStrategy)
                .Set(r => r.DefaultBranch, config.DefaultBranch)
                .Set(r => r.Quiet, config.Quiet);


            return collection.FindOneAndUpdateAsync<RepoConfig>(r => r.Owner == config.Owner && r.Repo == config.Repo, update, options);
        }
    }
}