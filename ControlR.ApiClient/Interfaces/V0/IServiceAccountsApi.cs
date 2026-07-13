using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

namespace ControlR.ApiClient.Interfaces.V0;

public interface IServiceAccountsApi
{
  Task<ApiResult<CreateServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, CreateServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<CreateServiceAccountResponseDto>> Create(CreateServiceAccountRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);
  Task<ApiResult<List<ServiceAccountDto>>> GetAll(CancellationToken cancellationToken = default);
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);
}
