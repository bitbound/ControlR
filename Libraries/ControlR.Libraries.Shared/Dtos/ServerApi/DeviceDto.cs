using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Collections.Immutable;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public record DeviceDto(
  [property: Key(0)] string Name,
  [property: Key(1)] string AgentVersion,
  [property: Key(2)] double CpuUtilization,
  [property: Key(3)] Guid Id,
  [property: Key(4)] bool Is64Bit,
  [property: Key(5)] bool IsOnline,
  [property: Key(6)] DateTimeOffset LastSeen,
  [property: Key(7)] Architecture OsArchitecture,
  [property: Key(9)] SystemPlatform Platform,
  [property: Key(10)] int ProcessorCount,
  [property: Key(11)] string ConnectionId,
  [property: Key(12)] string OsDescription,
  [property: Key(13)] Guid TenantId,
  [property: Key(14)] double TotalMemory,
  [property: Key(15)] double TotalStorage,
  [property: Key(16)] double UsedMemory,
  [property: Key(17)] double UsedStorage,
  [property: Key(18)] string[] CurrentUsers,
  [property: Key(19)] string[] MacAddresses,
  [property: Key(20)] string PublicIpV4,
  [property: Key(21)] string PublicIpV6,
  [property: Key(22)] IReadOnlyList<Drive> Drives) : IHasPrimaryKey
{
  [Key(23)]
  public ImmutableArray<Guid>? TagIds { get; set; }

  [Key(24)]
  public string? Alias { get; init; }

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}