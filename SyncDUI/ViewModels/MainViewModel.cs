using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncDUI.Models;
using SyncDUI.Services;

namespace SyncDUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SyncthingRestClient _client;

    [ObservableProperty]
    private string baseUrl = "http://localhost:8384";

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string versionSummary = "Not connected";

    [ObservableProperty]
    private string currentMachinePrettyName = "Local machine";

    [ObservableProperty]
    private string systemSummary = "Awaiting connection";

    [ObservableProperty]
    private string healthSummary = "Not checked";

    [ObservableProperty]
    private string pathsSummary = "Not checked";

    [ObservableProperty]
    private string configPath = string.Empty;

    [ObservableProperty]
    private string homePath = string.Empty;

    [ObservableProperty]
    private string deviceStatsSummary = "No device statistics loaded";

    [ObservableProperty]
    private string folderStatsSummary = "No folder statistics loaded";

    [ObservableProperty]
    private string configSummary = "No config loaded";

    [ObservableProperty]
    private string settingsSummary = "No settings loaded";

    [ObservableProperty]
    private ObservableCollection<string> eventLog = new();

    [ObservableProperty]
    private ObservableCollection<SyncthingConnectionEntry> devices = new();

    [ObservableProperty]
    private ObservableCollection<SyncthingFolderEntry> folders = new();

    [ObservableProperty]
    private Dictionary<string, string> deviceNameLookup = new();

    [ObservableProperty]
    private Dictionary<string, string> folderNameLookup = new();

    [ObservableProperty]
    private SyncthingConnectionEntry? selectedDevice;

    [ObservableProperty]
    private SyncthingConnectionEntry? localDevice;

    [ObservableProperty]
    private SyncthingFolderEntry? selectedFolder;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand<string?> OpenPathCommand { get; }

    public MainViewModel()
    {
        var discoveredApiKey = DiscoverApiKey();
        if (!string.IsNullOrWhiteSpace(discoveredApiKey))
        {
            apiKey = discoveredApiKey;
        }

        var discoveredUrl = DiscoverBaseUrl();
        if (!string.IsNullOrWhiteSpace(discoveredUrl))
        {
            baseUrl = discoveredUrl;
        }

        _client = new SyncthingRestClient(BaseUrl, ApiKey);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenPathCommand = new RelayCommand<string?>(OpenPathInFileManager);
    }

    private static string? DiscoverApiKey()
    {
        foreach (var path in GetCandidateConfigPaths())
        {
            try
            {
                var xml = XDocument.Load(path);
                var apikey = xml.Descendants("gui").Elements("apikey").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(apikey))
                {
                    return apikey.Trim();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception)
            {
            }
        }

        return Environment.GetEnvironmentVariable("SYNCTHING_API_KEY");
    }

    private static string? DiscoverBaseUrl()
    {
        foreach (var path in GetCandidateConfigPaths())
        {
            try
            {
                var xml = XDocument.Load(path);
                var address = xml.Descendants("gui").Elements("address").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(address))
                {
                    var host = address.Trim();
                    if (host.StartsWith("[", StringComparison.Ordinal))
                    {
                        host = host.Split(']')[0].TrimStart('[');
                    }
                    else if (host.Contains(':', StringComparison.Ordinal) && host.Count(ch => ch == ':') >= 1)
                    {
                        host = host.Substring(0, host.LastIndexOf(':'));
                    }

                    if (string.Equals(host, "0.0.0.0", StringComparison.Ordinal) ||
                        string.Equals(host, "::", StringComparison.Ordinal) ||
                        string.Equals(host, "[::]", StringComparison.OrdinalIgnoreCase))
                    {
                        host = "127.0.0.1";
                    }

                    var port = address.Contains(':', StringComparison.Ordinal)
                        ? address.Split(':').LastOrDefault()
                        : "8384";

                    if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(port))
                    {
                        return $"http://{host}:{port}";
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception)
            {
            }
        }

        return null;
    }

    private static string[] GetCandidateConfigPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var candidates = new[]
        {
            "/etc/syncthing/config.xml",
            "/var/lib/syncthing/config.xml",
            Path.Combine(home, ".local", "state", "syncthing", "config.xml"),
            Path.Combine(home, ".config", "syncthing", "config.xml"),
            Path.Combine(home, ".syncthing", "config.xml"),
            Path.Combine(xdg ?? Path.Combine(home, ".config"), "syncthing", "config.xml"),
        };

        return candidates.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray();
    }

    partial void OnBaseUrlChanged(string value)
    {
        _client.BaseUrl = value;
    }

    partial void OnApiKeyChanged(string value)
    {
        _client.ApiKey = value;
    }

    public async Task RefreshAsync()
    {
        try
        {
            var version = await _client.GetSystemVersionAsync();
            var status = await _client.GetSystemStatusAsync();
            var connections = await _client.GetSystemConnectionsAsync();
            var paths = await _client.GetSystemPathsAsync();
            var health = await _client.GetNoAuthHealthAsync();
            var deviceStats = await _client.GetStatsDeviceAsync();
            var folderStats = await _client.GetStatsFolderAsync();
            var events = await _client.GetEventsAsync(since: 0, timeout: 1, events: "DeviceConnected,FolderCompletion,ItemFinished");
            var config = await _client.GetConfigAsync();

            DeviceNameLookup.Clear();
            FolderNameLookup.Clear();
            if (config is not null && config.Value.ValueKind == JsonValueKind.Object)
            {
                PopulateDisplayNames(config.Value);
            }

            if (version is not null)
            {
                VersionSummary = $"{version.Version} ({version.Os}/{version.Arch})";
            }

            if (status is not null)
            {
                var uptime = TimeSpan.FromSeconds(status.Uptime);
                CurrentMachinePrettyName = TryGetPrettyNameForId(status.MyId, DeviceNameLookup, status.MyId);
                SystemSummary = string.Join(Environment.NewLine, new[]
                {
                    $"• Uptime: {uptime:c}",
                    $"• Goroutines: {status.Goroutines}",
                    $"• Memory alloc: {SyncthingConnectionEntry.FormatBytes(status.Alloc)}",
                    $"• Memory sys: {SyncthingConnectionEntry.FormatBytes(status.Sys)}",
                    $"• Machine: {CurrentMachinePrettyName}",
                    $"• CPU: {status.CpuPercent}%"
                });
                ConnectionStatus = "Connected";
            }

            HealthSummary = health is not null
                ? string.IsNullOrWhiteSpace(health.Error) ? "Healthy" : $"Health error: {health.Error}"
                : "Unavailable";

            ConfigPath = paths?.Config ?? string.Empty;
            HomePath = paths?.Home ?? string.Empty;
            PathsSummary = paths is not null
                ? $"Config: {paths.Config} · Home: {paths.Home}"
                : "Unavailable";

            var localDeviceStats = GetStatsForDeviceId(deviceStats, status?.MyId);
            DeviceStatsSummary = BuildNamedStatsSummary(localDeviceStats, DeviceNameLookup, "device");
            FolderStatsSummary = BuildNamedStatsSummary(folderStats, FolderNameLookup, "folder");

            ConfigSummary = config is null ? "No config loaded" : TruncateJson(config.Value);
            SettingsSummary = config is null ? "No settings loaded" : BuildSettingsSummary(config.Value);
            if (status is not null)
            {
                CurrentMachinePrettyName = TryGetPrettyNameForId(status.MyId, DeviceNameLookup, status.MyId);
            }

            EventLog.Clear();
            if (events is not null)
            {
                foreach (var item in events.Take(10))
                {
                    var eventType = string.IsNullOrWhiteSpace(item.Type) ? "event" : item.Type;
                    var detail = item.Data.ValueKind == JsonValueKind.Object ? item.Data.ToString() : item.Data.ToString();
                    EventLog.Add($"[{item.Time}] {eventType}: {Truncate(detail, 180)}");
                }
            }

            if (EventLog.Count == 0)
            {
                EventLog.Add("No recent events");
            }

            Devices.Clear();
            if (connections is not null)
            {
                foreach (var pair in connections.Connections.OrderBy(item => item.Key))
                {
                    var detail = pair.Value;
                    var displayName = TryGetPrettyNameForId(pair.Key, DeviceNameLookup, pair.Key);
                    Devices.Add(new SyncthingConnectionEntry
                    {
                        DeviceId = pair.Key,
                        Name = displayName,
                        Address = detail.Address,
                        Connected = detail.Connected,
                        ClientVersion = detail.ClientVersion,
                        Type = detail.Type,
                        InBytesTotal = detail.InBytesTotal,
                        OutBytesTotal = detail.OutBytesTotal,
                    });
                }
            }

            if (Devices.Count > 0)
            {
                LocalDevice = status is not null
                    ? Devices.FirstOrDefault(device => string.Equals(device.DeviceId, status.MyId, StringComparison.Ordinal))
                    : Devices.First();

                if (LocalDevice is null)
                {
                    LocalDevice = Devices.First();
                }

                SelectedDevice = Devices.FirstOrDefault(device => !string.Equals(device.DeviceId, status?.MyId ?? string.Empty, StringComparison.Ordinal))
                    ?? Devices.First();
            }
            else
            {
                LocalDevice = null;
                SelectedDevice = null;
            }

            Folders.Clear();
            if (config is not null && config.Value.ValueKind == JsonValueKind.Object)
            {
                if (config.Value.TryGetProperty("folders", out var foldersElement) && foldersElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var folderElement in foldersElement.EnumerateArray())
                    {
                        var folder = new SyncthingFolderEntry();

                        if (folderElement.TryGetProperty("id", out var idElement))
                        {
                            folder.Id = idElement.GetString() ?? string.Empty;
                        }

                        if (!string.IsNullOrWhiteSpace(folder.Id) && FolderNameLookup.TryGetValue(folder.Id, out var folderName))
                        {
                            folder.Name = folderName;
                        }

                        if (folderElement.TryGetProperty("path", out var pathElement))
                        {
                            folder.Path = pathElement.GetString() ?? string.Empty;
                        }

                        if (folderElement.TryGetProperty("type", out var typeElement))
                        {
                            folder.Type = typeElement.GetString() ?? string.Empty;
                        }

                        if (folderElement.TryGetProperty("readOnly", out var readOnlyElement))
                        {
                            folder.ReadOnly = readOnlyElement.GetBoolean();
                        }

                        if (folderElement.TryGetProperty("devices", out var devicesElement) && devicesElement.ValueKind == JsonValueKind.Array)
                        {
                            var deviceIds = new List<string>();
                            foreach (var deviceElement in devicesElement.EnumerateArray())
                            {
                                if (deviceElement.TryGetProperty("deviceID", out var deviceIdElement))
                                {
                                    var deviceId = deviceIdElement.GetString();
                                    if (!string.IsNullOrWhiteSpace(deviceId))
                                    {
                                        deviceIds.Add(deviceId);
                                    }
                                }
                            }

                            folder.Devices = string.Join(", ", deviceIds);
                        }

                        if (folderStats is not null && !string.IsNullOrWhiteSpace(folder.Id) && folderStats.TryGetValue(folder.Id, out var statsElement) && statsElement.ValueKind == JsonValueKind.Object)
                        {
                            var state = string.Empty;
                            var bytes = string.Empty;

                            if (statsElement.TryGetProperty("state", out var stateElement))
                            {
                                state = stateElement.ToString();
                            }

                            if (statsElement.TryGetProperty("globalBytes", out var globalBytesElement))
                            {
                                bytes = SyncthingConnectionEntry.FormatBytes(globalBytesElement.GetInt64());
                            }

                            if (string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(bytes))
                            {
                                folder.StatusSummary = "Folder loaded";
                            }
                            else
                            {
                                folder.StatusSummary = string.Join(" · ", new[]
                                {
                                    string.IsNullOrWhiteSpace(state) ? null : $"State: {state}",
                                    string.IsNullOrWhiteSpace(bytes) ? null : $"Global: {bytes}"
                                }.Where(value => !string.IsNullOrWhiteSpace(value)));
                            }
                        }
                        else
                        {
                            folder.StatusSummary = folder.ReadOnly ? "Read-only folder" : "Folder configured";
                        }

                        Folders.Add(folder);
                    }
                }
            }

            if (Folders.Count > 0)
            {
                SelectedFolder = Folders.First();
            }
            else
            {
                SelectedFolder = null;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
            VersionSummary = "Not connected";
            SystemSummary = "Connection failed";
            HealthSummary = "Unavailable";
            PathsSummary = "Unavailable";
            DeviceStatsSummary = "No device statistics loaded";
            FolderStatsSummary = "No folder statistics loaded";
            ConfigSummary = "No config loaded";
            SettingsSummary = "No settings loaded";
            ConfigPath = string.Empty;
            HomePath = string.Empty;
            CurrentMachinePrettyName = "Local machine";
            DeviceNameLookup.Clear();
            FolderNameLookup.Clear();
            LocalDevice = null;
            Devices.Clear();
            EventLog.Clear();
            EventLog.Add($"Error: {ex.Message}");
            SelectedDevice = null;
        }
    }

    private void PopulateDisplayNames(JsonElement config)
    {
        if (config.TryGetProperty("devices", out var devicesElement) && devicesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var deviceElement in devicesElement.EnumerateArray())
            {
                var deviceId = GetStringProperty(deviceElement, "deviceID");
                var name = GetStringProperty(deviceElement, "name");
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = GetStringProperty(deviceElement, "displayName");
                }

                DeviceNameLookup[deviceId] = string.IsNullOrWhiteSpace(name) ? deviceId : name;
            }
        }

        if (config.TryGetProperty("folders", out var foldersElement) && foldersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var folderElement in foldersElement.EnumerateArray())
            {
                var folderId = GetStringProperty(folderElement, "id");
                var name = GetStringProperty(folderElement, "label");
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = GetStringProperty(folderElement, "name");
                }

                FolderNameLookup[folderId] = string.IsNullOrWhiteSpace(name) ? folderId : name;
            }
        }
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private void OpenPathInFileManager(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var trimmed = path.Trim();
        if (!Directory.Exists(trimmed) && !File.Exists(trimmed))
        {
            trimmed = Path.GetDirectoryName(trimmed) ?? trimmed;
            if (string.IsNullOrWhiteSpace(trimmed) || (!Directory.Exists(trimmed) && !File.Exists(trimmed)))
            {
                return;
            }
        }

        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo("explorer.exe", trimmed);
            }
            else if (OperatingSystem.IsMacOS())
            {
                startInfo = new ProcessStartInfo("open", trimmed);
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-open", trimmed);
            }

            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
        catch (Exception)
        {
        }
    }

    private static Dictionary<string, JsonElement>? GetStatsForDeviceId(Dictionary<string, JsonElement>? stats, string? deviceId)
    {
        if (stats is null || string.IsNullOrWhiteSpace(deviceId))
        {
            return stats;
        }

        if (!stats.TryGetValue(deviceId, out var entry))
        {
            return stats;
        }

        return new Dictionary<string, JsonElement> { [deviceId] = entry };
    }

    private static string BuildNamedStatsSummary(Dictionary<string, JsonElement>? stats, Dictionary<string, string> displayNames, string itemType)
    {
        if (stats is null || stats.Count == 0)
        {
            return $"No {itemType} statistics received";
        }

        return string.Join(Environment.NewLine, stats
            .OrderBy(pair => pair.Key)
            .Take(12)
            .Select(pair =>
            {
                var label = TryGetPrettyNameForId(pair.Key, displayNames, pair.Key);
                var valueText = pair.Value.ValueKind switch
                {
                    JsonValueKind.Object => SummarizeNamedStatsObject(pair.Value),
                    JsonValueKind.Array => TruncateJson(pair.Value),
                    _ => pair.Value.ToString()
                };
                return string.IsNullOrWhiteSpace(valueText) ? $"• {label}" : $"• {label}: {valueText}";
            }));
    }

    private static string SummarizeNamedStatsObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var summary = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                summary.Add($"{property.Name}: {TruncateJson(property.Value)}");
                continue;
            }

            var value = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                summary.Add($"{property.Name}: {value}");
            }
        }

        return summary.Count == 0 ? string.Empty : string.Join(" · ", summary);
    }

    private static string TryGetPrettyNameForId(string id, Dictionary<string, string> displayNames, string fallback)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return fallback;
        }

        if (displayNames.TryGetValue(id, out var prettyName) && !string.IsNullOrWhiteSpace(prettyName))
        {
            return prettyName;
        }

        return id;
    }

    private static string BuildSettingsSummary(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object)
        {
            return "No settings available";
        }

        var pieces = new List<string>();

        if (TryReadString(config, "version", out var version))
        {
            pieces.Add($"Version: {version}");
        }

        if (TryReadProperty(config, "gui", out var gui) && gui.ValueKind == JsonValueKind.Object)
        {
            var guiSummary = new List<string>();
            if (TryReadString(gui, "address", out var guiAddress)) guiSummary.Add($"Address: {guiAddress}");
            if (TryReadString(gui, "user", out var guiUser)) guiSummary.Add($"User: {guiUser}");
            if (TryReadString(gui, "theme", out var guiTheme)) guiSummary.Add($"Theme: {guiTheme}");
            if (TryReadString(gui, "apikey", out var apiKeyValue) && !string.IsNullOrWhiteSpace(apiKeyValue)) guiSummary.Add("API key: configured");
            if (guiSummary.Count > 0)
            {
                pieces.Add("GUI: " + string.Join(" · ", guiSummary));
            }
        }

        if (TryReadProperty(config, "options", out var options) && options.ValueKind == JsonValueKind.Object)
        {
            var optionSummary = new List<string>();
            if (TryReadBoolean(options, "globalAnnEnabled", out var globalAnn)) optionSummary.Add($"Global discovery: {FormatBool(globalAnn, "on", "off")}");
            if (TryReadBoolean(options, "localAnnEnabled", out var localAnn)) optionSummary.Add($"Local discovery: {FormatBool(localAnn, "on", "off")}");
            if (TryReadBoolean(options, "relaysEnabled", out var relays)) optionSummary.Add($"Relays: {FormatBool(relays, "enabled", "disabled")}");
            if (TryReadString(options, "listenAddresses", out var listenAddresses)) optionSummary.Add($"Listen: {listenAddresses}");
            if (TryReadNumber(options, "maxSendKbps", out var sentKbps)) optionSummary.Add($"Send limit: {sentKbps} KB/s");
            if (TryReadNumber(options, "maxRecvKbps", out var recvKbps)) optionSummary.Add($"Recv limit: {recvKbps} KB/s");
            if (optionSummary.Count > 0)
            {
                pieces.Add("Options: " + string.Join(" · ", optionSummary));
            }
        }

        if (TryReadProperty(config, "folders", out var foldersElement))
        {
            pieces.Add($"Folders: {(foldersElement.ValueKind == JsonValueKind.Array ? foldersElement.GetArrayLength() : 0)}");
        }

        if (TryReadProperty(config, "devices", out var devicesElement))
        {
            pieces.Add($"Devices: {(devicesElement.ValueKind == JsonValueKind.Array ? devicesElement.GetArrayLength() : 0)}");
        }

        return pieces.Count > 0 ? string.Join(Environment.NewLine, pieces) : "Settings loaded but no summary fields were recognized.";
    }

    private static bool TryReadProperty(JsonElement element, string propertyName, out JsonElement property)
        => element.TryGetProperty(propertyName, out property);

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        if (TryReadProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        if (TryReadProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (TryReadProperty(element, propertyName, out property) && property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryReadNumber(JsonElement element, string propertyName, out long value)
    {
        if (TryReadProperty(element, propertyName, out var property) && property.ValueKind is JsonValueKind.Number)
        {
            value = property.GetInt64();
            return true;
        }

        value = 0;
        return false;
    }

    private static string FormatBool(bool value, string trueText, string falseText)
        => value ? trueText : falseText;

    private static string TruncateJson(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Truncate(json, 400);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}
