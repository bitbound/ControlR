using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ILogonTokensApi
{
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
