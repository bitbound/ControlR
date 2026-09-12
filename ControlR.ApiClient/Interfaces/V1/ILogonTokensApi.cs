using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ILogonTokensApi
{
  [ApiRoute($"{HttpConstants.V1.LogonTokensEndpoint}/external", "POST")]
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonTokenForExternal(CreateLogonTokenForExternalRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V1.LogonTokensEndpoint}/user", "POST")]
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonTokenForUser(CreateLogonTokenForUserRequestDto request, CancellationToken cancellationToken = default);
}
