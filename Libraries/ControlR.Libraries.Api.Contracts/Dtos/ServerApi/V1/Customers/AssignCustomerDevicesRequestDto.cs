using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

public record AssignCustomerDevicesRequestDto(
  [property: Required]
  [property: MaxLength(DtoLimits.DeviceIdsMaxCount)]
  IReadOnlyList<Guid> DeviceIds,

  [property: Required]
  [property: MaxLength(DtoLimits.DeviceIdsMaxCount)]
  IReadOnlyList<Guid> RemoveDeviceIds);