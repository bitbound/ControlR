using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Public;
using ControlR.Libraries.Api.Contracts.Enums;
using System.Net.Http.Json;

namespace ControlR.ApiClient;

internal partial class PublicApi
{
  async Task<ApiResult<BundleMetadataDto>> IAgentUpdateApi.GetBundleMetadata(RuntimeId runtime, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<BundleMetadataDto>(
        $"{HttpConstants.AgentUpdateEndpoint}/get-bundle-metadata/{runtime}",
        cancellationToken));
  }
}