using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private string systemSummary = "Awaiting connection";

    [ObservableProperty]
    private ObservableCollection<SyncthingConnectionEntry> devices = new();

    [ObservableProperty]
    private SyncthingConnectionEntry? selectedDevice;

    public IAsyncRelayCommand RefreshCommand { get; }

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

            if (version is not null)
            {
                VersionSummary = $"{version.Version} ({version.Os}/{version.Arch})";
            }

            if (status is not null)
            {
                var uptime = TimeSpan.FromSeconds(status.Uptime);
                SystemSummary = $"Uptime {uptime:c} · {status.Goroutines} goroutines · {status.MyId}";
                ConnectionStatus = "Connected";
            }

            Devices.Clear();
            if (connections is not null)
            {
                foreach (var pair in connections.Connections.OrderBy(item => item.Key))
                {
                    var detail = pair.Value;
                    Devices.Add(new SyncthingConnectionEntry
                    {
                        DeviceId = pair.Key,
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
                SelectedDevice = Devices.First();
            }
            else
            {
                SelectedDevice = null;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
            VersionSummary = "Not connected";
            SystemSummary = "Connection failed";
            Devices.Clear();
            SelectedDevice = null;
        }
    }
}
