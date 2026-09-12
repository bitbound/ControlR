using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ILogonTokensApi
{
  [ApiRoute($"{HttpConstants.Internal.LogonTokensEndpoint}", "POST")]
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
