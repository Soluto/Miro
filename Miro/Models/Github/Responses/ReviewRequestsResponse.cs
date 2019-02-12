using System.Collections.Generic;
using Miro.Models.Github.Entities;

namespace Miro.Models.Github.Responses
{
    public class RequestedReviewersResponse
    {
        public List<User> Users { get; set; } = new List<User>();
        public List<Team> Teams { get; set; } = new List<Team>();
    }
}