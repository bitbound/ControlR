using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  async Task<ApiResult<BundleMetadataDto>> IInternalAgentUpdateApi.GetBundleMetadata(RuntimeId runtime, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<BundleMetadataDto>(
        $"{HttpConstants.Internal.AgentUpdateEndpoint}/get-bundle-metadata/{runtime}",
        cancellationToken));
  }
}
