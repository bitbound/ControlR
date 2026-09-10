using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITenantServiceAccountsApi
{
  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}/credentials", "POST")]
  Task<ApiResult<InternalDtos.CreateTenantServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, InternalDtos.CreateTenantServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}", "POST")]
  Task<ApiResult<InternalDtos.TenantServiceAccountDto>> Create(InternalDtos.CreateTenantServiceAccountRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}", "DELETE")]
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}", "GET")]
  Task<ApiResult<InternalDtos.TenantServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}", "GET")]
  Task<ApiResult<InternalDtos.TenantServiceAccountDto[]>> GetAll(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}/purge", "DELETE")]
  Task<ApiResult> PurgeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}", "DELETE")]
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.TenantServiceAccountsEndpoint}/{{serviceAccountId}}", "PUT")]
  Task<ApiResult<InternalDtos.TenantServiceAccountDto>> Update(Guid serviceAccountId, InternalDtos.UpdateTenantServiceAccountRequestDto request, CancellationToken cancellationToken = default);
}
