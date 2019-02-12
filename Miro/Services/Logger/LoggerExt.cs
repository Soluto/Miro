using Miro.Models.Merge;
using Serilog;

namespace Miro.Services.Logger
{
    public static class LoggerExt
    {
        public static ILogger WithExtraData(this ILogger logger, object extraData)
        {
            return logger.ForContext("ExtraData", extraData, true);
        }

         public static ILogger WithMergeRequestData(this ILogger logger, MergeRequest mergeRequest)
        {
            return logger.ForContext("MergeRequestData", new 
            {
                owner = mergeRequest.Owner, 
                repo = mergeRequest.Repo, 
                branch = mergeRequest.Branch, 
                prId = mergeRequest.PrId, 
                sha = mergeRequest.Sha,
                title = mergeRequest.Title,
                receivedMergeCommand = mergeRequest.ReceivedMergeCommand,
            });
        }
        
    }
}