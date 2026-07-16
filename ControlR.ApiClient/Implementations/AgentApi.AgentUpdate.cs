using ControlR.ApiClient.Interfaces.Agent;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.ApiClient;

internal partial class AgentApi
{
  async Task<ApiResult<BundleMetadataDto>> IAgentUpdateApi.GetBundleMetadata(RuntimeId runtime, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<BundleMetadataDto>(
        $"{HttpConstants.Agent.UpdatesEndpoint}/get-bundle-metadata/{runtime}",
        cancellationToken));
  }
}
