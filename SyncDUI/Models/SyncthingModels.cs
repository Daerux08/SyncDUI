using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace SyncDUI.Models;

public sealed class SyncthingVersionResponse
{
    [JsonPropertyName("arch")] public string Arch { get; set; } = string.Empty;
    [JsonPropertyName("os")] public string Os { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("longVersion")] public string LongVersion { get; set; } = string.Empty;
}

public sealed class SyncthingStatusResponse
{
    [JsonPropertyName("alloc")] public long Alloc { get; set; }
    [JsonPropertyName("sys")] public long Sys { get; set; }
    [JsonPropertyName("goroutines")] public int Goroutines { get; set; }
    [JsonPropertyName("cpuPercent")] public int CpuPercent { get; set; }
    [JsonPropertyName("uptime")] public long Uptime { get; set; }
    [JsonPropertyName("startTime")] public string StartTime { get; set; } = string.Empty;
    [JsonPropertyName("pathSeparator")] public string PathSeparator { get; set; } = string.Empty;
    [JsonPropertyName("tilde")] public string Tilde { get; set; } = string.Empty;
    [JsonPropertyName("myID")] public string MyId { get; set; } = string.Empty;
    [JsonPropertyName("discoveryEnabled")] public bool DiscoveryEnabled { get; set; }
    [JsonPropertyName("discoveryMethods")] public int DiscoveryMethods { get; set; }
    [JsonPropertyName("themes")] public List<string> Themes { get; set; } = new();
}

public sealed class SyncthingConnectionTotals
{
    [JsonPropertyName("at")] public string At { get; set; } = string.Empty;
    [JsonPropertyName("inBytesTotal")] public long InBytesTotal { get; set; }
    [JsonPropertyName("outBytesTotal")] public long OutBytesTotal { get; set; }
}

public sealed class SyncthingConnectionDetail
{
    [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    [JsonPropertyName("at")] public string At { get; set; } = string.Empty;
    [JsonPropertyName("clientVersion")] public string ClientVersion { get; set; } = string.Empty;
    [JsonPropertyName("connected")] public bool Connected { get; set; }
    [JsonPropertyName("inBytesTotal")] public long InBytesTotal { get; set; }
    [JsonPropertyName("outBytesTotal")] public long OutBytesTotal { get; set; }
    [JsonPropertyName("isLocal")] public bool IsLocal { get; set; }
    [JsonPropertyName("paused")] public bool Paused { get; set; }
    [JsonPropertyName("startedAt")] public string StartedAt { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
}

public sealed class SyncthingConnectionsResponse
{
    [JsonPropertyName("connections")] public Dictionary<string, SyncthingConnectionDetail> Connections { get; set; } = new();
    [JsonPropertyName("total")] public SyncthingConnectionTotals Total { get; set; } = new();
}

public sealed class SyncthingConnectionEntry
{
    public string DeviceId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool Connected { get; set; }
    public string ClientVersion { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long InBytesTotal { get; set; }
    public long OutBytesTotal { get; set; }

    public string StatusLabel => Connected ? "Connected" : "Disconnected";
    public string Throughput => $"{FormatBytes(InBytesTotal)} ↓ / {FormatBytes(OutBytesTotal)} ↑";

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";

        double value = bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        int index = 0;

        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[index]}";
    }
}
