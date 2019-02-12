using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Miro.Tests.Helpers
{
    public class GithubApiMock
    {
        private static string GithubUrl =  Environment.GetEnvironmentVariable("GITHUB_API_URL") ?? "http://localhost:3000"; 

        public static async Task<string> MockGithubCall(string method, string url, string requestBody, string mockResponse, bool isJson = true)
        {
            var simpleFakeServerRequest = new HttpRequestMessage(HttpMethod.Post, $"{GithubUrl}/fake_server_admin/calls");
            var mockCommentCall = new
            {
                method = method,
                url = url,
                body = requestBody,
                response = mockResponse,
                isJson = isJson
            };

            simpleFakeServerRequest.Content = new StringContent(JsonConvert.SerializeObject(mockCommentCall), Encoding.UTF8, "application/json");

            var response = await new HttpClient().SendAsync(simpleFakeServerRequest);
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<MockedCall>(content);
            return json.CallId;
        }

        public static Task<string> MockGithubCall(string method, string url, string mockResponse, bool isJson = true) => MockGithubCall(method, url, null, mockResponse, isJson);
        public static async Task<string> MockGithubCall(string method, string url, string requestBody, int statusCode)
        {
            var simpleFakeServerRequest = new HttpRequestMessage(HttpMethod.Post, $"{GithubUrl}/fake_server_admin/calls");
            var mockCommentCall = new
            {
                body = requestBody,
                method = method,
                url = url,
                statusCode = statusCode
            };

            simpleFakeServerRequest.Content = new StringContent(JsonConvert.SerializeObject(mockCommentCall), Encoding.UTF8, "application/json");

            var response = await new HttpClient().SendAsync(simpleFakeServerRequest);
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<MockedCall>(content);
            return json.CallId;
        }
        
        public static async Task<CallCheck<T>> GetCall<T>(string callId)
        {
            var httpClient = new HttpClient();
            var url = $"{GithubUrl}/fake_server_admin/calls?callId={callId}";

            var getMadeCallRequest = new HttpRequestMessage(HttpMethod.Get, url);
            var madeCallResponse = await httpClient.SendAsync(getMadeCallRequest);
            var madeCallResult = await madeCallResponse.Content.ReadAsStringAsync();
            var jsonAssertResult = JsonConvert.DeserializeObject<CallCheck<T>>(madeCallResult);

            return jsonAssertResult;
        }

         public static async Task<CallCheck<Dictionary<string, string>>> GetCall(string callId)
        {
            var httpClient = new HttpClient();
            var url = $"{GithubUrl}/fake_server_admin/calls?callId={callId}";

            var getMadeCallRequest = new HttpRequestMessage(HttpMethod.Get, url);
            var madeCallResponse = await httpClient.SendAsync(getMadeCallRequest);
            var madeCallResult = await madeCallResponse.Content.ReadAsStringAsync();
            var jsonAssertResult = JsonConvert.DeserializeObject<CallCheck<Dictionary<string, string>>>(madeCallResult);

            return jsonAssertResult;
        }

        public static async Task ResetGithubMock()
        {
            var httpClient = new HttpClient();
            var url = $"{GithubUrl}/fake_server_admin/calls";

            var getMadeCallRequest = new HttpRequestMessage(HttpMethod.Delete, url);
            await httpClient.SendAsync(getMadeCallRequest);
        }
    }

    public class MockedCall
    {
        public string CallId { get; set; }
    }

    public class CallCheck<T>
    {
        public bool HasBeenMade { get; set; }
        public CallDetails<T> Details { get; set; }
    }

    public class CallDetails<T>
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public T Body { get; set; }
    }
}