using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<PublicRegistrationSettings>> IPublicRegistrationSettingsApi.GetPublicRegistrationSettings(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
      await _client.GetFromJsonAsync<PublicRegistrationSettings>(HttpConstants.PublicRegistrationSettingsEndpoint, cancellationToken));
  }
}
