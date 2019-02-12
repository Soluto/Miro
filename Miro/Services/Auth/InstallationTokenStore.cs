using System;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Miro.Services.Auth
{
    public class InstallationTokenStore : IDisposable
    {
        private IConnectableObservable<string> tokenObservable;
        private IDisposable subscription;

        public InstallationTokenStore(IConfiguration configuration)
        {
            var isProductionEnv = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT").Equals("Production", StringComparison.OrdinalIgnoreCase);
            var pemVar = configuration.GetValue("GITHUB_PEM_SECRET", "nothing");

            if (isProductionEnv && pemVar == "nothing" || string.IsNullOrEmpty(pemVar))
            {
                throw new ArgumentException("You must provide a pem file path when running in production");
            }
            if (pemVar == "nothing" || string.IsNullOrEmpty(pemVar))
            {
                tokenObservable = new string[] { "dummy token" }.ToObservable().Replay(1);
            }
            else
            {
                var githubInstallationId = configuration["GITHUB_INSTALLATION_ID"];
                tokenObservable = Observable.Interval(TimeSpan.FromMinutes(55))
                                             .StartWith(0)
                                             .Select(l => Unit.Default)
                                             .ObserveOn(TaskPoolScheduler.Default)
                                             .SelectMany(_ => GetInstallationToken(pemVar, githubInstallationId))
                                             .Replay(1);

            }

            subscription = tokenObservable.Connect();
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        public async Task<string> GetToken()
        {
            return await tokenObservable.FirstAsync();
        }

        private async Task<string> GetInstallationToken(string pemData, string githubInstallationId)
        {
            var generator = new GitHubJwt.GitHubJwtFactory(new StringPrivateKeySource(pemData),
                                                           new GitHubJwtFactoryOptions
                                                           {
                                                               AppIntegrationId = 15348,
                                                               ExpirationSeconds = 600 // 10 minutes is the maximum time allowed
                                                           });

            var jwtToken = generator.CreateEncodedJwtToken();
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://api.github.com/") };

            var installationTokenRequest = new HttpRequestMessage
            {
                RequestUri = new Uri($"/app/installations/{githubInstallationId}/access_tokens", UriKind.Relative),
                Method = HttpMethod.Post
            };

            installationTokenRequest.Headers.Add("Authorization", $"Bearer {jwtToken}");
            installationTokenRequest.Headers.Add("User-Agent", $"Miro");
            installationTokenRequest.Headers.Add("Accept", $"application/vnd.github.machine-man-preview+json");

            var tokenResponse = await httpClient.SendAsync(installationTokenRequest);
            var responseString = await tokenResponse.Content.ReadAsStringAsync();
            var responseJson = JsonConvert.DeserializeObject<dynamic>(responseString);

            return responseJson["token"];
        }
    }
}