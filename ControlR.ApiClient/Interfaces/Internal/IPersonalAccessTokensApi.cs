using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPersonalAccessTokensApi
{
  [ApiRoute($"{HttpConstants.Internal.PersonalAccessTokensEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.PersonalAccessTokens.CreatePersonalAccessToken (POST /api/v1/personal-access-tokens?tenantId=), which requires tenantId and returns 201. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.PersonalAccessTokensEndpoint}/{{personalAccessTokenId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.PersonalAccessTokens.DeletePersonalAccessToken (DELETE /api/v1/personal-access-tokens/{id}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeletePersonalAccessToken(Guid personalAccessTokenId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.PersonalAccessTokensEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.PersonalAccessTokens.GetPersonalAccessTokens (GET /api/v1/personal-access-tokens?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetPersonalAccessTokens(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.PersonalAccessTokensEndpoint}/{{personalAccessTokenId}}", "PUT")]
  [Obsolete("Use ControlrApi.V1.PersonalAccessTokens.UpdatePersonalAccessToken (PUT /api/v1/personal-access-tokens/{id}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
