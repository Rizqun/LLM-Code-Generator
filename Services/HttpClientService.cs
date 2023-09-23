using System.Net.Http.Headers;
using System.Text.Json;

namespace CodeGenerator.Services
{
    public class HttpClientService
    {
        private readonly string _baseUrl;
        public HttpClientService(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public async Task<TResult> GetWithBasicAuthTokenAsync<TResult>(string basicAuthToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
            var response = await client.GetAsync(_baseUrl);

            var result = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var resultObj = JsonSerializer.Deserialize<TResult>(result, options);

            return resultObj!;
        }
    }
}
