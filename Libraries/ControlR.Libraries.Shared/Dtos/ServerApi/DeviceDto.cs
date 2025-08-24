using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Collections.Immutable;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record DeviceDto(
  string Name,
  string AgentVersion,
  double CpuUtilization,
  Guid Id,
  bool Is64Bit,
  bool IsOnline,
  DateTimeOffset LastSeen,
  Architecture OsArchitecture,
  SystemPlatform Platform,
   int ProcessorCount,
   string ConnectionId,
   string OsDescription,
   Guid TenantId,
   double TotalMemory,
   double TotalStorage,
   double UsedMemory,
   double UsedStorage,
   string[] CurrentUsers,
   string[] MacAddresses,
   string PublicIpV4,
   string PublicIpV6,
   string LocalIpV4,
   string LocalIpV6,
   IReadOnlyList<Drive> Drives) : IHasPrimaryKey
{
  public ImmutableArray<Guid>? TagIds { get; set; }

  public string? Alias { get; init; }

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}