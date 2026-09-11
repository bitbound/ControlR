using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using DGDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDeviceGroupsApi
{
  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}/members?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AddDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, DGDtos.AddDeviceGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DGDtos.DeviceGroupDetailDto>> CreateDeviceGroup(Guid tenantId, DGDtos.CreateDeviceGroupRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DGDtos.DeviceGroupsResponseDto>> GetAllDeviceGroups(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DGDtos.DeviceGroupDetailDto>> GetDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}/members?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> RemoveDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, DGDtos.RemoveDeviceGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<DGDtos.DeviceGroupDetailDto>> UpdateDeviceGroup(Guid deviceGroupId, Guid tenantId, DGDtos.UpdateDeviceGroupRequestDto request, CancellationToken cancellationToken = default);
}