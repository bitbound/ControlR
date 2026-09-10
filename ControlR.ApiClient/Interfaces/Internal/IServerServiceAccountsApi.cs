using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using InternalDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerServiceAccountsApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials", "POST")]
  Task<ApiResult<InternalDtos.CreateServerServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, InternalDtos.CreateServerServiceAccountCredentialRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}", "POST")]
  Task<ApiResult<InternalDtos.ServerServiceAccountDto>> Create(InternalDtos.CreateServerServiceAccountRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "DELETE")]
  Task<ApiResult> Delete(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "GET")]
  Task<ApiResult<InternalDtos.ServerServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}", "GET")]
  Task<ApiResult<InternalDtos.ServerServiceAccountDto[]>> GetAll(CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}/purge", "DELETE")]
  Task<ApiResult> PurgeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}/credentials/{{credentialId}}", "DELETE")]
  Task<ApiResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.Internal.ServerServiceAccountsEndpoint}/{{serviceAccountId}}", "PUT")]
  Task<ApiResult<InternalDtos.ServerServiceAccountDto>> Update(Guid serviceAccountId, InternalDtos.UpdateServerServiceAccountRequestDto request, CancellationToken cancellationToken = default);
}
