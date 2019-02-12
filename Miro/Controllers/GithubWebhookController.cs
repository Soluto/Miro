using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Miro.Models.Github.IncomingEvents;
using Miro.Models.Github.Responses;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Logger;
using Miro.Services.Merge;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Miro.Controllers
{
    public class GithubWebhookController : ControllerBase
    {
        private readonly ILogger logger = Log.ForContext<GithubWebhookController>();

        Dictionary<string, Func<JObject, Task<WebhookResponse>>> handlers;

        public GithubWebhookController(
            IssueCommentEventHandler issueCommentEventHandler,
            PullRequestReviewEventHandler pullRequestReviewEventHandler,
            PullRequestEventHandler pullRequestEventHandler,
            PushEventHandler pushEventHandler,
            StatusEventHandler statusEventHandler)
        {
            handlers = new Dictionary<string, Func<JObject, Task<WebhookResponse>>> {
                {"issue_comment", data => issueCommentEventHandler.Handle(data.ToObject<IssueCommentEvent>())},
                {"pull_request_review", data => pullRequestReviewEventHandler.Handle(data.ToObject<PullRequestReviewEvent>())},
                {"pull_request", data => pullRequestEventHandler.Handle(data.ToObject<PullRequestEvent>())},
                {"status", data => statusEventHandler.Handle(data.ToObject<StatusEvent>())},
                {"push", data => pushEventHandler.Handle(data.ToObject<PushEvent>())},
            };
        }

        [GitHubWebHook]
        public async Task<IActionResult> Post(string id, string @event, JObject data)
        {
            try
            {
                if (handlers.ContainsKey(@event))
                {
                    var res = await handlers[@event](data);
                    return StatusCode(200, res);
                }

                return StatusCode(200);
            }
            catch (Exception e)
            {
                logger.WithExtraData(data).Error(e, $"Could not handle Event {@event}");
                return StatusCode(500, "There was an error");
            }
        }
    }
}
