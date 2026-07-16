using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Agent;

public interface IAgentDevicesApi
{
  [ApiRoute($"{HttpConstants.Agent.DevicesEndpoint}", "POST")]
  Task<ApiResult> CreateDevice(InternalDtos.CreateDeviceRequestDto request, CancellationToken cancellationToken = default);
}
