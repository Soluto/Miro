using System.Threading.Tasks;
using Miro.Models.Github.Responses;

namespace Miro.Services.Github.EventHandlers
{

    public interface IWebhookEventHandler
    {

    }

    public interface IWebhookEventHandler<T> : IWebhookEventHandler
    {
        Task<WebhookResponse> Handle(T payload);
    }
}