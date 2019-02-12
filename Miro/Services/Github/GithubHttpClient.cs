using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Miro.Services.Auth;
using Newtonsoft.Json;

namespace Miro.Services.Github
{
    public class GithubHttpClient
    {
        private readonly InstallationTokenStore tokenStore;
        private string githubInstallationId;
        private HttpClient httpClient;

        public GithubHttpClient(IConfiguration configuration, InstallationTokenStore tokenStore)
        {
            var githubApiUrl = configuration.GetValue("GITHUB_API_URL", "https://api.github.com/");
            httpClient = new HttpClient() { BaseAddress = new Uri(githubApiUrl) };
            this.tokenStore = tokenStore;
        }

        public async Task<T> Get<T>(string uri)
        {
            var request = await CreateGithubHttpRequest(uri);
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<T>();
        }

        public async Task<HttpResponseMessage> Post(string uri, object payload)
        {
            var request = await CreateGithubHttpRequest(uri);
            request.Method = HttpMethod.Post;
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            return await httpClient.SendAsync(request);
        }

         public async Task<HttpResponseMessage> Delete(string uri, object payload = null)
        {
            var request = await CreateGithubHttpRequest(uri);
            request.Method = HttpMethod.Delete;
            if ( payload != null ) request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return await httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> Put(string uri, object payload)
        {
            var request = await CreateGithubHttpRequest(uri);
            request.Method = HttpMethod.Put;
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            return await httpClient.SendAsync(request);
        }

        private async Task<HttpRequestMessage> CreateGithubHttpRequest(string uri)
        {
            var accessToken = await tokenStore.GetToken();
            var httpRequest = new HttpRequestMessage() { RequestUri = new Uri(uri, UriKind.Relative) };
            httpRequest.Headers.Add("Authorization", new List<string> { $"token {accessToken}" });
            httpRequest.Headers.Add("User-Agent", new List<string> { "Miro" });

            return httpRequest;
        }
    }
}