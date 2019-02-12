using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Miro.Tests.Helpers
{
    public class MergeRequestsCollection
    {
        private static string MongoUrl =  Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); 

        public MergeRequestsCollection()
        {
            var client = new MongoClient(MongoUrl ?? "mongodb://localhost:27017");
            var database = client.GetDatabase("miro-db");
            this.Collection = database.GetCollection<BsonDocument>("merge-requests");
        }

        public IMongoCollection<BsonDocument> Collection { get; }

        public async Task Insert(string owner, string repo, int prId, string branch = "some-branch", bool receivedMergeCommand = false, IEnumerable<CheckStatus> Checks = null, string sha = null, bool isFork = false)
        {
            var existingMergeRequest = new BsonDocument();
            existingMergeRequest["Owner"] = owner;
            existingMergeRequest["Repo"] = repo;
            existingMergeRequest["PrId"] = prId;
            existingMergeRequest["Branch"] = branch ?? "some-branch";
            existingMergeRequest["ReceivedMergeCommand"] = receivedMergeCommand;
            existingMergeRequest["IsFork"] = isFork;

            if (sha != null)
            {
                existingMergeRequest["Sha"] = sha;
            }

            if (Checks != null) {
                var BsonChecks = Checks.Select(x => new BsonDocument {{ "Name", x.Name },{ "Status", x.Status }});
                existingMergeRequest["Checks"] = new BsonArray().AddRange(BsonChecks);
            }

            await Collection.InsertOneAsync(existingMergeRequest);
        }

        public async Task UpdateMergeRequest(string owner, string repo, int prId, string key, object value)
        {
             var update = Builders<BsonDocument>.Update
                .Set(r => r[key], value);
           await Collection.FindOneAndUpdateAsync<BsonDocument>(r => r["Owner"] == owner &&
                                                 r["Repo"] == repo &&
                                                 r["PrId"] == prId, update);
        }

        public Task UpdateMergeRequest(string owner, string repo, int prId, bool receivedMergeCommand, DateTime mergeCommandTime)
        {
            return Task.WhenAll(
                UpdateMergeRequest(owner, repo, prId, "ReceivedMergeCommand", receivedMergeCommand), 
                UpdateMergeRequest(owner, repo, prId, "ReceivedMergeCommandTimestamp", mergeCommandTime));
        }

        public async Task InsertWithTestChecksSuccessAndMergeCommand(string owner, string repo, int prId, string branch = "some-branch", string sha = null, bool isFork = false)
        {
            await Insert(owner, repo, prId, branch, true, new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                },
                new CheckStatus {
                Name = Consts.TEST_CHECK_B,
                Status = "success"
                }
            }, sha, isFork);
        }

         public async Task InsertWithTestChecksSuccess(string owner, string repo, int prId, string branch = "some-branch", string sha = null, bool isFork = false)
        {
            await Insert(owner, repo, prId, branch, false, new List<CheckStatus>(){
                new CheckStatus {
                Name = Consts.TEST_CHECK_A,
                Status = "success"
                },
                new CheckStatus {
                Name = Consts.TEST_CHECK_B,
                Status = "success"
                }
            }, sha, isFork);
        }
    }
}