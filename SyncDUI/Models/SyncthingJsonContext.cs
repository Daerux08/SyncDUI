using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncDUI.Models;

[JsonSerializable(typeof(SyncthingVersionResponse))]
[JsonSerializable(typeof(SyncthingStatusResponse))]
[JsonSerializable(typeof(SyncthingConnectionsResponse))]
[JsonSerializable(typeof(SyncthingSystemPathsResponse))]
[JsonSerializable(typeof(SyncthingHealthResponse))]
[JsonSerializable(typeof(SyncthingEvent))]
[JsonSerializable(typeof(List<SyncthingEvent>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, SyncthingConnectionDetail>))]
[JsonSerializable(typeof(SyncthingConnectionDetail))]
[JsonSerializable(typeof(SyncthingConnectionTotals))]
internal partial class SyncthingJsonContext : JsonSerializerContext
{
}