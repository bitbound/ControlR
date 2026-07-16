using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IServiceAccountsApi
{
  [ApiRoute("POST", "/api/v0/service-accounts/{serviceAccountId}/credentials")]
  Task<ApiResult<CreateServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, CreateServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/v0/service-accounts")]
  Task<ApiResult<CreateServiceAccountResponseDto>> Create(CreateServiceAccountRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/v0/service-accounts/{serviceAccountId}")]
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/service-accounts/{serviceAccountId}")]
  Task<ApiResult<ServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/v0/service-accounts")]
  Task<ApiResult<List<ServiceAccountDto>>> GetAll(CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/v0/service-accounts/{serviceAccountId}/credentials/{credentialId}")]
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);
}
