using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Shared.Web.Rin;

public interface IRinClient
{
    Task<string> GetAsync(string path, IHeaderDictionary headers);
    Task<string> PostAsync(string path, object data, IHeaderDictionary headers);
}

public class RinClient : IRinClient
{
    private readonly HttpClient _httpClient;
    private readonly RinSettings _settings;

    public RinClient(HttpClient httpClient, IOptions<RinSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        if (!string.IsNullOrEmpty(_settings?.WebApiUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.WebApiUrl);
        }
    }

    public async Task<string> GetAsync(string path, IHeaderDictionary headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        ForwardHeaders(request, headers);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> PostAsync(string path, object data, IHeaderDictionary headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        ForwardHeaders(request, headers);
        request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private void ForwardHeaders(HttpRequestMessage request, IHeaderDictionary headers)
    {
        if (headers == null)
        {
            return;
        }

        foreach (var header in headers)
        {
            if (header.Key.StartsWith("X-Red5-", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }
}