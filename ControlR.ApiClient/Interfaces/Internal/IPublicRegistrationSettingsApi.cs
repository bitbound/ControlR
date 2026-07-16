using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPublicRegistrationSettingsApi
{
  [ApiRoute("GET", "/api/internal/public-registration-settings")]
  Task<ApiResult<PublicRegistrationSettings>> GetPublicRegistrationSettings(CancellationToken cancellationToken = default);
}
