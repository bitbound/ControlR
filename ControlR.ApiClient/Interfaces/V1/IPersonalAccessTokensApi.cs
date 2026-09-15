using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IPersonalAccessTokensApi
{
  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(Guid tenantId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{{id}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeletePersonalAccessToken(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PersonalAccessTokensResponseDto>> GetPersonalAccessTokens(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{{id}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdatePersonalAccessToken(Guid id, Guid tenantId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
