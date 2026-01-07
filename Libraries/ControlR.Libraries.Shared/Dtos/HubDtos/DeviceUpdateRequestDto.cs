using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DeviceUpdateRequestDto(
    Guid Id,
    Guid TenantId, // Required for bootstrapping
    string Name,
    string AgentVersion,
    bool Is64Bit,
    Architecture OsArchitecture,
    string OsDescription,
    SystemPlatform Platform,
    int ProcessorCount,
    double CpuUtilization,
    double TotalMemory,
    double TotalStorage,
    double UsedMemory,
    double UsedStorage,
    string[] CurrentUsers,
    string[] MacAddresses,
    string LocalIpV4,
    string LocalIpV6,
    IReadOnlyList<Drive> Drives
);
