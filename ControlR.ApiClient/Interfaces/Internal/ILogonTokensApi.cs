using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ILogonTokensApi
{
  Task<ApiResult<InternalDtos.LogonTokenResponseDto>> CreateLogonToken(InternalDtos.LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}
