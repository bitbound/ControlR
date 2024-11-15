using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public record DeviceDto(
  [property: MsgPackKey] string Name,
  [property: MsgPackKey] string AgentVersion,
  [property: MsgPackKey] double CpuUtilization,
  [property: MsgPackKey] Guid Id,
  [property: MsgPackKey] bool Is64Bit,
  [property: MsgPackKey] bool IsOnline,
  [property: MsgPackKey] DateTimeOffset LastSeen,
  [property: MsgPackKey] Architecture OsArchitecture,
  [property: MsgPackKey] SystemPlatform Platform,
  [property: MsgPackKey] int ProcessorCount,
  [property: MsgPackKey] string ConnectionId,
  [property: MsgPackKey] string OsDescription,
  [property: MsgPackKey] Guid TenantId,
  [property: MsgPackKey] double TotalMemory,
  [property: MsgPackKey] double TotalStorage,
  [property: MsgPackKey] double UsedMemory,
  [property: MsgPackKey] double UsedStorage,
  [property: MsgPackKey] string[] CurrentUsers,
  [property: MsgPackKey] string[] MacAddresses,
  [property: MsgPackKey] string PublicIpV4,
  [property: MsgPackKey] string PublicIpV6,
  [property: MsgPackKey] IReadOnlyList<Drive> Drives) : IHasPrimaryKey
{
  [MsgPackKey]
  public Guid[]? TagIds { get; set; }

  [MsgPackKey]
  public string? Alias { get; init; }

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}