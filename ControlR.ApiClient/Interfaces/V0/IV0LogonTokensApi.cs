using ControlR.Libraries.Api.Contracts.Dtos;
using V0Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IV0LogonTokensApi
{
  Task<ApiResult<V0Dtos.LogonTokenResponseDto>> CreateLogonToken(V0Dtos.CreateLogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
