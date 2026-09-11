using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using PATDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IPersonalAccessTokensApi
{
  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<PATDtos.CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(Guid tenantId, PATDtos.CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{{id}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeletePersonalAccessToken(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PATDtos.PersonalAccessTokensResponseDto>> GetPersonalAccessTokens(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.PersonalAccessTokensEndpoint}/{{id}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PATDtos.PersonalAccessTokenResponseDto>> UpdatePersonalAccessToken(Guid id, Guid tenantId, PATDtos.UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
