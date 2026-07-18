using System.Runtime.CompilerServices;
using ControlR.ApiClient.Interfaces.V0;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient;

internal partial class V0Api
{
  async Task<ApiResult<List<V0Dtos.DesktopSessionResponseDto>>> IDesktopSessionsApi.GetActiveDesktopSessions(
    Guid deviceId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(
        $"{HttpConstants.V0.DevicesEndpoint}/{deviceId}/desktop-sessions/{deviceId}",
        cancellationToken);

      await response.EnsureSuccessStatusCodeWithDetails();

      var list = await response.Content
        .ReadFromJsonAsync<List<V0Dtos.DesktopSessionResponseDto>>(cancellationToken);

      return list ?? [];
    });
  }
}