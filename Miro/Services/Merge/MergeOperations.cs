using System;
using System.Linq;
using System.Threading.Tasks;
using Miro.Models.Merge;
using Miro.Services.Checks;
using Miro.Services.Comments;
using Miro.Services.Github;
using Miro.Services.Logger;
using Serilog;

namespace Miro.Services.Merge
{
    public class MergeOperations
    {
        private readonly PrMerger prMerger;
        private readonly PrUpdater prUpdater;
        private readonly CommentCreator commentCreator;
        private readonly MergeRequestsRepository mergeRequestRepository;
        private readonly MiroMergeCheck miroMergeCheck;
        private readonly MergeabilityValidator mergeabilityValidator;

        private readonly ILogger logger = Log.ForContext<MergeOperations>();

        public MergeOperations(
                            PrMerger prMerger,
                            PrUpdater prUpdater,
                            CommentCreator commentCreator,
                            MergeRequestsRepository mergeRequestRepository,
                            MiroMergeCheck miroMergeCheck,
                            MergeabilityValidator mergeabilityValidator)
        {
            this.prMerger = prMerger;
            this.prUpdater = prUpdater;
            this.commentCreator = commentCreator;
            this.mergeRequestRepository = mergeRequestRepository;
            this.miroMergeCheck = miroMergeCheck;
            this.mergeabilityValidator = mergeabilityValidator;
        }

        public async Task<bool> TryToMerge(MergeRequest mergeRequest)
        {
            if (mergeRequest == null)
            {
                logger.Warning($"Received TryToMerge command from a null mergeRequest");
                throw new Exception("Can not merge, PR is not defined");
            }

            if (!mergeRequest.ReceivedMergeCommand)
            {
                logger.WithMergeRequestData(mergeRequest).Information($"PR can't be merged, missing merge command");
                return false;
            }

            var mergeabilityValidationErrors = await mergeabilityValidator.ValidateMergeability(mergeRequest);

            if (mergeabilityValidationErrors.Any())
            {
                logger.WithMergeRequestData(mergeRequest).Information($"PR can't be merged, found mergeability validation errors");
                return false;
            }
            
            return await MergeOrUpdateBranch(mergeRequest);
        }


        private async Task TryToUpdateBranch(string owner, string repo, int prId, string branch)
        {
            var extraLogData = new { owner, repo, branch };
            try
            {
                await prUpdater.UpdateBranch(owner, repo, branch);
            }
            catch (Exception er)
            {
                logger.WithExtraData(extraLogData).Error(er, $"Could not update branch for PR due to exception from the github api");
                await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.CantUpdateBranchHeader, er.Message, CommentsConsts.CantUpdateBranchBody);
            }
        }

        private async Task<bool> MergeOrUpdateBranch(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;
            var branch = mergeRequest.Branch;

            logger.WithMergeRequestData(mergeRequest).Information($"A READY merge request for PR found, merging");
            await commentCreator.CreateListedComment(owner, repo, prId, CommentsConsts.Merging, mergeRequest.Checks.Select(x => $":heavy_check_mark: {x.Name}").ToList());
            try
            {
                await prMerger.Merge(mergeRequest);
                await mergeRequestRepository.UpdateState(owner, repo, prId, "MERGED");
                return true;
            }
            catch (PullRequestMismatchException prEx)
            {
                if (mergeRequest.IsFork)
                {
                    await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.PullRequestCanNotBeMerged, CommentsConsts.UpdatingAForkNotAllowed);
                    return false;
                }
                logger.WithMergeRequestData(mergeRequest).Information(prEx, $"Could not merge PR due to PullRequestMismatchException exception");
                await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.PullRequestCanNotBeMerged, prEx.Message, CommentsConsts.TryToUpdateWithDefaultBranch);
                await TryToUpdateBranch(owner, repo, prId, mergeRequest.Branch);
                return false;

            }
            catch (Exception e)
            {
                logger.WithMergeRequestData(mergeRequest).Error(e, $"Could not merge PR due to unknown exception from github API");
                await commentCreator.CreateComment(owner, repo, prId, CommentsConsts.PullRequestCanNotBeMerged, e.Message);
                return false;
            }
        }

    }
}