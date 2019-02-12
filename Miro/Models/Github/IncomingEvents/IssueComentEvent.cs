using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.IncomingEvents
{
    public class IssueCommentEvent
    {
        public string Action { get; set; }
        public Comment Comment { get; set; }
        public Issue Issue { get; set; }
        public User Sender { get; set; }
        public Repository Repository { get; set; }
    }
}