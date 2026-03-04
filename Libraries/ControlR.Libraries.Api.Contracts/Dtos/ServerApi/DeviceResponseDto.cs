using ControlR.Libraries.Api.Contracts.Enums;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record DeviceResponseDto(
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
   IReadOnlyList<Drive> Drives,
   bool IsOutdated)
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