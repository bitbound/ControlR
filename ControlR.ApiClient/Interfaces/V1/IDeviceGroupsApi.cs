using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IDeviceGroupsApi
{
  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}/members?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AddDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, AddDeviceGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<DeviceGroupDetailDto>> CreateDeviceGroup(Guid tenantId, CreateDeviceGroupRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DeviceGroupsResponseDto>> GetAllDeviceGroups(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<DeviceGroupDetailDto>> GetDeviceGroup(Guid deviceGroupId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}/members?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> RemoveDeviceGroupMembers(Guid deviceGroupId, Guid tenantId, RemoveDeviceGroupMembersRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.DeviceGroupsEndpoint}/{{deviceGroupId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<DeviceGroupDetailDto>> UpdateDeviceGroup(Guid deviceGroupId, Guid tenantId, UpdateDeviceGroupRequestDto request, CancellationToken cancellationToken = default);
}