using System.Collections.Generic;
using Miro.Models.Github.Entities;
using Newtonsoft.Json;

namespace Miro.Models.Github.Responses
{
    public class WebhookResponse
    {
        public WebhookResponse(bool handled, string message)
        {
            this.Handled = handled;
            this.Message = message;
        }

        public bool Handled { get; set; }
        public string Message { get; set; }
    }
}