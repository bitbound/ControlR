using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.ServiceAccounts;
using Microsoft.Extensions.Logging;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult<CreateServiceAccountCredentialResponseDto>> IServiceAccountsApi.AddCredential(
    Guid serviceAccountId,
    CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(
        $"{HttpConstants.ServiceAccountsEndpoint}/{serviceAccountId}/credentials", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountCredentialResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<CreateServiceAccountResponseDto>> IServiceAccountsApi.Create(
    CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.ServiceAccountsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CreateServiceAccountResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult> IServiceAccountsApi.Delete(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.ServiceAccountsEndpoint}/{serviceAccountId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async IAsyncEnumerable<ServiceAccountDto> IServiceAccountsApi.GetAll([EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<ServiceAccountDto>(
      HttpConstants.ServiceAccountsEndpoint,
      cancellationToken: cancellationToken);

    await foreach (var account in stream.WithCancellation(cancellationToken))
    {
      if (account is null)
      {
        continue;
      }

      if (!_options.Value.DisableStreamingResponseDtoStrictness)
      {
        var validationErrors = DtoValidatorFactory.Validate(account);
        if (validationErrors is not null)
        {
          if (_options.Value.DisableResponseDtoStrictness)
          {
            _logger.LogWarning("Streaming response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
          }
          else
          {
            throw new InvalidDataException($"DTO validation failed: {validationErrors}");
          }
        }
      }

      yield return account;
    }
  }

  async Task<ApiResult> IServiceAccountsApi.RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.DeleteAsync(
        $"{HttpConstants.ServiceAccountsEndpoint}/{serviceAccountId}/credentials/{credentialId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
