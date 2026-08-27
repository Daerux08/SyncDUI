using System;
using System.Collections.Generic;
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
    private static readonly JsonTypeInfo<SyncthingSystemPathsResponse> PathsInfo = SyncthingJsonContext.Default.SyncthingSystemPathsResponse;
    private static readonly JsonTypeInfo<SyncthingHealthResponse> HealthInfo = SyncthingJsonContext.Default.SyncthingHealthResponse;
    private static readonly JsonTypeInfo<List<SyncthingEvent>> EventsInfo = SyncthingJsonContext.Default.ListSyncthingEvent;
    private static readonly JsonTypeInfo<Dictionary<string, JsonElement>> JsonMapInfo = SyncthingJsonContext.Default.DictionaryStringJsonElement;

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

    public async Task<SyncthingSystemPathsResponse?> GetSystemPathsAsync()
        => await GetJsonAsync("/rest/system/paths", PathsInfo);

    public async Task<SyncthingHealthResponse?> GetNoAuthHealthAsync()
        => await GetJsonAsync("/rest/noauth/health", HealthInfo, includeApiKey: false);

    public async Task<Dictionary<string, JsonElement>?> GetStatsDeviceAsync()
        => await GetJsonAsync("/rest/stats/device", JsonMapInfo);

    public async Task<Dictionary<string, JsonElement>?> GetStatsFolderAsync()
        => await GetJsonAsync("/rest/stats/folder", JsonMapInfo);

    public async Task<List<SyncthingEvent>?> GetEventsAsync(int? since = null, int? timeout = null, string? events = null)
    {
        var query = new List<string>();
        if (since.HasValue) query.Add($"since={since.Value}");
        if (timeout.HasValue) query.Add($"timeout={timeout.Value}");
        if (!string.IsNullOrWhiteSpace(events)) query.Add($"events={Uri.EscapeDataString(events)}");

        var uri = "/rest/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await GetJsonAsync(uri, EventsInfo);
    }

    public async Task<JsonElement?> GetConfigAsync()
        => await GetJsonElementAsync("/rest/config");

    public async Task<string?> PingAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/rest/system/ping");
        return response;
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUri, JsonTypeInfo<T> typeInfo, bool includeApiKey = true)
    {
        var body = await SendAsync(HttpMethod.Get, relativeUri, includeApiKey);
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize(body, typeInfo);
    }

    private async Task<JsonElement?> GetJsonElementAsync(string relativeUri, bool includeApiKey = true)
    {
        var body = await SendAsync(HttpMethod.Get, relativeUri, includeApiKey);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private async Task<string> SendAsync(HttpMethod method, string relativeUri, bool includeApiKey = true)
    {
        var request = new HttpRequestMessage(method, GetAbsoluteUri(relativeUri));

        if (includeApiKey && !string.IsNullOrWhiteSpace(ApiKey))
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
