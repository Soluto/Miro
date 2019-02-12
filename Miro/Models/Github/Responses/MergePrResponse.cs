using System.Collections.Generic;
using Miro.Models.Github.Entities;

namespace Miro.Models.Github.Responses
{
    public class MergePrResponse
    {
        public bool Merged { get; set; }
        public string Message { get; set; } = "unknown";
    }
}