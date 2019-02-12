using System;

namespace Miro.Models.Checks
{
    public class CheckStatus
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string TargetUrl { get; set; }
    }
}