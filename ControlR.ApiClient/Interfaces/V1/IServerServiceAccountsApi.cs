using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IServerServiceAccountsApi
{
  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials", "POST")]
  Task<ApiResult<CreateServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, CreateServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}", "POST")]
  Task<ApiResult<ServerServiceAccountDto>> Create(CreateServerServiceAccountRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "DELETE")]
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "GET")]
  Task<ApiResult<ServerServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}", "GET")]
  Task<ApiResult<ServerServiceAccountsResponseDto>> GetAll(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}/purge", "DELETE")]
  Task<ApiResult> PurgeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}", "DELETE")]
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "PUT")]
  Task<ApiResult<ServerServiceAccountDto>> Update(Guid serviceAccountId, UpdateServiceAccountRequestDto request, CancellationToken cancellationToken = default);
}
