using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Miro.Models.Github.Entities;
using Miro.Models.Github.Responses;

namespace Miro.Services.Github
{
    public class ReviewsRetriever
    {
        private readonly GithubHttpClient githubHttpClient;

        public ReviewsRetriever(GithubHttpClient githubHttpClient)
        {
            this.githubHttpClient = githubHttpClient;
        }
        public async Task<RequestedReviewersResponse> GetRequestedReviewers(string owner, string repo, int prId)
        {
            var reviewRequests = await githubHttpClient.Get<RequestedReviewersResponse>($"/repos/{owner}/{repo}/pulls/{prId}/requested_reviewers");

            return reviewRequests;
        }

        public async Task<List<Review>> GetReviews(string owner, string repo, int prId)
        {
            var reviews = await githubHttpClient.Get<List<Review>>($"/repos/{owner}/{repo}/pulls/{prId}/reviews");

            return reviews;
        }
    }
}