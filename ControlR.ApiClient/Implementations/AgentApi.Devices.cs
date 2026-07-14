using ControlR.ApiClient.Interfaces.Agent;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient;

internal partial class AgentApi
{
  async Task<ApiResult> IAgentDevicesApi.CreateDevice(InternalDtos.CreateDeviceRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(HttpConstants.Agent.DevicesEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
