using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IServiceAccountsApi
{
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}/{{serviceAccountId}}/credentials", "POST")]
  Task<ApiResult<CreateServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, CreateServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}", "POST")]
  Task<ApiResult<CreateServiceAccountResponseDto>> Create(CreateServiceAccountRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}/{{serviceAccountId}}", "DELETE")]
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}/{{serviceAccountId}}", "GET")]
  Task<ApiResult<ServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}", "GET")]
  Task<ApiResult<List<ServiceAccountDto>>> GetAll(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.V0.ServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}", "DELETE")]
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);
}
