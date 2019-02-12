using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Miro.Tests.Helpers
{
    public class RepoConfigurationCollection
    {
        private static string MongoUrl =  Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); 

        public RepoConfigurationCollection()
        {
            var client = new MongoClient(MongoUrl ?? "mongodb://localhost:27017");
            var database = client.GetDatabase("miro-db");
            this.Collection = database.GetCollection<BsonDocument>("repo-config");
        }

        public IMongoCollection<BsonDocument> Collection { get; }

        public async Task Insert(string owner, string repo, bool deleteAfterMerge = false, string updateBranchStrategy = "oldest", string mergePolicy = "blacklist", string defaultBranch = "master")
        {
            var repoConfig = new BsonDocument();
            repoConfig["Owner"] = owner;
            repoConfig["Repo"] = repo;
            repoConfig["DeleteAfterMerge"] = deleteAfterMerge;
            repoConfig["UpdateBranchStrategy"] = updateBranchStrategy;
            repoConfig["MergePolicy"] = mergePolicy;
            repoConfig["UpdatedAt"] = DateTime.UtcNow;
            repoConfig["DefaultBranch"] = defaultBranch;
            await Collection.InsertOneAsync(repoConfig);
        }

        public async Task<BsonDocument> Get(string owner, string repo)
        {
           return await Collection.Find<BsonDocument>(r => r["Owner"] == owner).FirstOrDefaultAsync();
        }
    }
}