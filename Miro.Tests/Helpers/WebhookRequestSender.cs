using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Miro.Tests.Helpers
{
    public class WebhookRequestSender
    {
        private static string ServerUrl =  Environment.GetEnvironmentVariable("SERVER_URL"); 

        public static async Task SendWebhookRequest(string eventType, string payload)
        {
            const string secret = "614d038b841a4846e27a92cc4b25ce5e54e1ae4a"; // match the secret defined in docker-compose for testing
            var webhookRequest = new HttpRequestMessage(HttpMethod.Post, $"{(ServerUrl ?? "http://localhost:5000")}/api/webhooks/incoming/github");
            var signature = await ComputeGithubSignature(secret, payload);
            webhookRequest.Headers.Add("X-Hub-Signature", $"sha1={signature}");
            webhookRequest.Headers.Add("X-Github-Event", eventType);
            webhookRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(webhookRequest);
            response.EnsureSuccessStatusCode();
        }

        public static async Task<string> ComputeGithubSignature(string secret, string @event)
        {
            byte[] secretKey = Encoding.UTF8.GetBytes(secret);

            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(@event);
                await writer.FlushAsync();
                stream.Position = 0;

                var hasher = new HMACSHA1(secretKey);
                return hasher.ComputeHash(stream).Aggregate("", (s, e) => s + string.Format("{0:x2}", e), s => s);
            }
        }
    }
}