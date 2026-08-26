using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using SyncDUI.Models;

namespace SyncDUI.Services;

public sealed class SyncthingRestClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonTypeInfo<SyncthingVersionResponse> VersionInfo = SyncthingJsonContext.Default.SyncthingVersionResponse;
    private static readonly JsonTypeInfo<SyncthingStatusResponse> StatusInfo = SyncthingJsonContext.Default.SyncthingStatusResponse;
    private static readonly JsonTypeInfo<SyncthingConnectionsResponse> ConnectionsInfo = SyncthingJsonContext.Default.SyncthingConnectionsResponse;

    public string BaseUrl { get; set; } = "http://localhost:8384";
    public string ApiKey { get; set; } = string.Empty;

    public SyncthingRestClient(string baseUrl, string apiKey)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        ApiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<SyncthingVersionResponse?> GetSystemVersionAsync()
        => await GetJsonAsync("/rest/system/version", VersionInfo);

    public async Task<SyncthingStatusResponse?> GetSystemStatusAsync()
        => await GetJsonAsync("/rest/system/status", StatusInfo);

    public async Task<SyncthingConnectionsResponse?> GetSystemConnectionsAsync()
        => await GetJsonAsync("/rest/system/connections", ConnectionsInfo);

    public async Task<string?> PingAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/rest/system/ping");
        return response;
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUri, JsonTypeInfo<T> typeInfo)
    {
        var body = await SendAsync(HttpMethod.Get, relativeUri);
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize(body, typeInfo);
    }

    private async Task<string> SendAsync(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, GetAbsoluteUri(relativeUri));

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            request.Headers.Add("X-API-Key", ApiKey);
        }

        using var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Syncthing request failed ({(int)response.StatusCode}): {content}");
        }

        return content;
    }

    private Uri GetAbsoluteUri(string relativeUri)
    {
        var normalizedBase = BaseUrl.TrimEnd('/');
        if (!normalizedBase.EndsWith("/rest", StringComparison.OrdinalIgnoreCase))
        {
            normalizedBase += "/rest";
        }

        var normalizedRelative = relativeUri.TrimStart('/');
        if (normalizedRelative.StartsWith("rest/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRelative = normalizedRelative[5..];
        }

        return new Uri($"{normalizedBase.TrimEnd('/')}/{normalizedRelative}");
    }
}
