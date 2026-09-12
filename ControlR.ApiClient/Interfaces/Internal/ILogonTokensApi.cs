using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ILogonTokensApi
{
  [ApiRoute($"{HttpConstants.Internal.LogonTokensEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.LogonTokens.CreateLogonTokenForUser (POST /api/v1/logon-tokens/user), which requires tenantId in the request body, or CreateLogonTokenForExternal (POST /api/v1/logon-tokens/external) for server-scoped callers. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
