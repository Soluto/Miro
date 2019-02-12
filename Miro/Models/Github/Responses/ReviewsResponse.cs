using System.Collections.Generic;
using Miro.Models.Github.Entities;

namespace Miro.Models.Github.Responses
{
    class ReviewesResponse
    {
        public List<Review> Reviews { get; set; }
    }
}