using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPublicRegistrationSettingsApi
{
  Task<ApiResult<PublicRegistrationSettings>> GetPublicRegistrationSettings(CancellationToken cancellationToken = default);
}
