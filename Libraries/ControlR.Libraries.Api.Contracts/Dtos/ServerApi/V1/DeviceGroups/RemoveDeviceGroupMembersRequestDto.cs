using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public record RemoveDeviceGroupMembersRequestDto(
  [property: Required]
  [property: MaxLength(DtoLimits.DeviceIdsMaxCount)]
  IReadOnlyList<Guid> DeviceIds);