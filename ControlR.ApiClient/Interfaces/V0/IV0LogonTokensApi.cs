using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IV0LogonTokensApi
{
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(IssueLogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
