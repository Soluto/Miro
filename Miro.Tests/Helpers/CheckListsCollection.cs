using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Miro.Tests.Helpers
{
    public class CheckListsCollection
    {
        private static string MongoUrl =  Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); 

        private string[] defaultChecks = new string[]{Consts.TEST_CHECK_A, Consts.TEST_CHECK_B};
        public CheckListsCollection()
        {
            var client = new MongoClient(MongoUrl ?? "mongodb://localhost:27017");
            var database = client.GetDatabase("miro-db");
            this.Collection = database.GetCollection<BsonDocument>("check-lists");
        }

        public IMongoCollection<BsonDocument> Collection { get; }

        public async Task Insert(string owner, string repo, IEnumerable<string> CheckNames = null)
        {
            var existingMergeRequest = new BsonDocument();
            existingMergeRequest["Owner"] = owner;
            existingMergeRequest["Repo"] = repo;

            if (CheckNames != null) {
                existingMergeRequest["CheckNames"] = new BsonArray(CheckNames);
            }

            await Collection.InsertOneAsync(existingMergeRequest);
        }

         public Task InsertWithDefaultChecks(string owner, string repo) => Insert(owner, repo, defaultChecks);
    }
}