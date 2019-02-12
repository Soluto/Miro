using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miro.Models.Merge;
using Miro.Models.Validation;
using Miro.Services.Checks;
using Miro.Services.Github;
using Miro.Services.Logger;
using Serilog;

namespace Miro.Services.Merge
{
    public class MergeabilityValidator
    {
        private readonly ReviewsRetriever reviewsRetriever;
        private readonly ILogger logger = Log.ForContext<MergeabilityValidator>();
        private readonly ChecksManager checksManager;

        public MergeabilityValidator(ReviewsRetriever reviewsRetriever, ChecksManager checksManager)
        {
            this.reviewsRetriever = reviewsRetriever;
            this.checksManager = checksManager;
        }

        public async Task<List<ValidationError>> ValidateMergeability(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;
            var errors = new List<ValidationError>();

            var pendingReviewsError = await ValidateNoPendingReviews(owner, repo, prId);
            if (pendingReviewsError != null)
            {
                errors.Add(pendingReviewsError);
            }

            var changesRequestedError = await ValidateNoChangesRequested(mergeRequest);
            if (changesRequestedError != null)
            {
                errors.Add(changesRequestedError);
            }

            var missingChecksError = await ValidateNoChecksMissing(mergeRequest);
            if (missingChecksError != null)
            {
                errors.Add(missingChecksError);
            }
            return errors;
        }

        private async Task<ValidationError> ValidateNoPendingReviews(string owner, string repo, int prId)
        {
            logger.WithExtraData(new {owner, repo, prId}).Information("Checking if PR has pending reviews");
            var requestedReviewers = await reviewsRetriever.GetRequestedReviewers(owner, repo, prId);

            if (requestedReviewers.Teams.Any() || requestedReviewers.Users.Any())
            {
                logger.WithExtraData(new {owner, repo, prId}).Information("PR has pending reviews");
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Still waiting for a review from: ");
                requestedReviewers.Teams.ForEach(t => stringBuilder.AppendLine(t.Name));
                requestedReviewers.Users.ForEach(u => stringBuilder.AppendLine(u.Login));

                return new ValidationError { Error = stringBuilder.ToString() };
            }

            return null;
        }

        private async Task<ValidationError> ValidateNoChecksMissing(MergeRequest mergeRequest)
        {
            logger.WithMergeRequestData(mergeRequest).Information("Checking if PR has missing checks");

            var missingChecks = await checksManager.GetMissingChecks(mergeRequest.Owner, mergeRequest.Repo, mergeRequest.Checks);
            if (missingChecks.Any())
            {
                logger.WithMergeRequestData(mergeRequest).Information($"PR has missing checks");
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Pending status checks: ");
                missingChecks.ForEach(c => stringBuilder.AppendLine(c));

                return new ValidationError { Error = stringBuilder.ToString() };
            }
            return null;
        }

        private async Task<ValidationError> ValidateNoChangesRequested(MergeRequest mergeRequest)
        {
            var owner = mergeRequest.Owner;
            var repo = mergeRequest.Repo;
            var prId = mergeRequest.PrId;

            logger.WithMergeRequestData(mergeRequest).Information("Checking if PR has changes requested");
            var reviews = await reviewsRetriever.GetReviews(owner, repo, prId);
            var latestReviewPerUser = reviews.GroupBy(r => r.User.Id)
                                             .Select(g => g.OrderByDescending(r => r.SubmittedAt)
                                                           .First());

            var changesRequestedReviews = latestReviewPerUser.Where(r => r.State == "CHANGES_REQUESTED");
            if (changesRequestedReviews.Any())
            {
                logger.WithMergeRequestData(mergeRequest).Information("PR has requested changes");
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Changes requested by: ");
                changesRequestedReviews.ToList().ForEach(r => stringBuilder.AppendLine(r.User.Login));

                return new ValidationError { Error = stringBuilder.ToString() };
            }

            return null;
        }
    }
}