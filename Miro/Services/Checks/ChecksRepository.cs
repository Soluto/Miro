using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Miro.Models.Checks;
using Miro.Models.Merge;
using MongoDB.Driver;

namespace Miro.Services.Checks
{
    public class ChecksRepository
    {
        private readonly IMongoCollection<CheckList> collection;

        public ChecksRepository(IMongoCollection<CheckList> collection)
        {
            this.collection = collection;
        }

        public async Task<CheckList> Get(string owner, string repo)
        {
            return await collection.Find(r => r.Owner == owner && r.Repo == repo).FirstOrDefaultAsync();
        }

        public async Task<CheckList> Update(string owner, string repo, List<string> checks)
        {
            var options = new FindOneAndUpdateOptions<CheckList>
            {
                IsUpsert = true
            };
                                                 
            var update = Builders<CheckList>.Update
            .Set(r => r.CheckNames, checks)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

            return await collection.FindOneAndUpdateAsync<CheckList>(r => r.Owner == owner && r.Repo == repo, update, options);
        }

        public async Task Create(CheckList checksCollection)
        {
            await collection.InsertOneAsync(checksCollection);
        }
    }
}