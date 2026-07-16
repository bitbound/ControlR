using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPersonalAccessTokensApi
{
  [ApiRoute("POST", "/api/internal/personal-access-tokens")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/personal-access-tokens/{personalAccessTokenId}")]
  Task<ApiResult> DeletePersonalAccessToken(Guid personalAccessTokenId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/personal-access-tokens")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetPersonalAccessTokens(CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/personal-access-tokens/{personalAccessTokenId}")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
