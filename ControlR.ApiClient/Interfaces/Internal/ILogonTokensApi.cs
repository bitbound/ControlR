using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ILogonTokensApi
{
  [ApiRoute($"{HttpConstants.Internal.LogonTokensEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.LogonTokens.CreateLogonTokenForUser (POST /api/v1/logon-tokens/user) when the token is for a ControlR user, or CreateLogonTokenForExternal (POST /api/v1/logon-tokens/external) when its subject is an external desktop principal identified by a correlation id rather than a ControlR user id. Both require tenantId in the request body and both accept tenant callers. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
