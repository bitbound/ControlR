using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPersonalAccessTokensApi
{
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeletePersonalAccessToken(Guid personalAccessTokenId, CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenDto[]>> GetPersonalAccessTokens(CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
