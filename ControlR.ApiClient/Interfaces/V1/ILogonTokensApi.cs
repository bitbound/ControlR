using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using V1Dtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ILogonTokensApi
{
  [ApiRoute($"{HttpConstants.V1.LogonTokensEndpoint}/external", "POST")]
  Task<ApiResult<V1Dtos.LogonTokenResponseDto>> CreateLogonTokenForExternal(V1Dtos.CreateLogonTokenForExternalRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V1.LogonTokensEndpoint}/user", "POST")]
  Task<ApiResult<V1Dtos.LogonTokenResponseDto>> CreateLogonTokenForUser(V1Dtos.CreateLogonTokenForUserRequestDto request, CancellationToken cancellationToken = default);
}
