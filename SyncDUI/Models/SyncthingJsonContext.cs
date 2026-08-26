using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SyncDUI.Models;

[JsonSerializable(typeof(SyncthingVersionResponse))]
[JsonSerializable(typeof(SyncthingStatusResponse))]
[JsonSerializable(typeof(SyncthingConnectionsResponse))]
[JsonSerializable(typeof(Dictionary<string, SyncthingConnectionDetail>))]
[JsonSerializable(typeof(SyncthingConnectionDetail))]
[JsonSerializable(typeof(SyncthingConnectionTotals))]
internal partial class SyncthingJsonContext : JsonSerializerContext
{
}