using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  async Task<ApiResult> IAuthApi.ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/change-password", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/forgot-password", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.LogOut(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/logout", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/reset-password", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }
}
