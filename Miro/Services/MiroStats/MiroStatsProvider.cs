using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Miro.Services.Checks;
using Miro.Services.Logger;
using Miro.Services.Merge;
using MiroConfig;
using Serilog;

namespace Miro.Services.MiroStats
{
    public class MiroStatsProvider
    {
        private readonly MergeRequestsRepository mergeRequestsRepository;
        private readonly ChecksRepository checksRepository;
        private readonly RepoConfigRepository repoConfigRepository;
        private readonly ILogger logger = Log.ForContext<MiroStatsProvider>();


        public MiroStatsProvider(
            MergeRequestsRepository mergeRequestsRepository, 
            ChecksRepository checksRepository,
            RepoConfigRepository repoConfigRepository)
        {
            this.mergeRequestsRepository = mergeRequestsRepository;
            this.checksRepository = checksRepository;
            this.repoConfigRepository = repoConfigRepository;
        }

        public async Task<MiroStats> Get()
        {
                var allConfigs = await repoConfigRepository.Get();
                var allRepos = allConfigs.GroupBy(x => $"{x.Owner}/{x.Repo}");

                var count = allRepos.Count();
                var names = new List<string>();

                foreach (var group in allRepos)
                {
                    names.Add(group.Key);
                }
                
                var response = new MiroStats{
                    RepoNames = names,
                    NumOfRepos = count
                };
                logger.WithExtraData(response).Information("Calculated Miro Stats");
                return response;
        }
        
    }

        public class MiroStats
        {
            public int NumOfRepos {get; set;}
            public List<string> RepoNames {get; set;} = new List<string>();
        }
}