using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

public partial class ControlrApi
{
  private const string ChangePasswordEndpoint = $"{HttpConstants.AuthEndpoint}/change-password";
  private const string ConfirmEmailEndpoint = $"{HttpConstants.AuthEndpoint}/confirmEmail";
  private const string ForgotPasswordEndpoint = $"{HttpConstants.AuthEndpoint}/forgotPassword";
  private const string InteractiveLoginEndpoint = $"{HttpConstants.AuthEndpoint}/interactive-login";
  private const string ManageInfoEndpoint = $"{HttpConstants.AuthEndpoint}/manage/info";
  private const string RegisterEndpoint = $"{HttpConstants.AuthEndpoint}/register";
  private const string ResendConfirmationEmailEndpoint = $"{HttpConstants.AuthEndpoint}/resendConfirmationEmail";
  private const string ResetPasswordEndpoint = $"{HttpConstants.AuthEndpoint}/resetPassword";
  private const string TwoFactorEndpoint = $"{HttpConstants.AuthEndpoint}/manage/2fa";

  async Task<ApiResult> IAuthApi.ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(ChangePasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<string>> IAuthApi.ConfirmEmail(Guid userId, string code, string? changedEmail, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      var endpoint = $"{ConfirmEmailEndpoint}?userId={Uri.EscapeDataString(userId.ToString())}&code={Uri.EscapeDataString(code)}";
      if (!string.IsNullOrWhiteSpace(changedEmail))
      {
        endpoint += $"&changedEmail={Uri.EscapeDataString(changedEmail)}";
      }

      using var response = await _client.GetAsync(endpoint, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsStringAsync(cancellationToken);
    });
  }

  async Task<ApiResult> IAuthApi.ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(ForgotPasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ManageInfoResponseDto>> IAuthApi.GetManageInfo(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.GetAsync(ManageInfoEndpoint, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ManageInfoResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<AccessTokenResponseDto>> IAuthApi.LogIn(LoginRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/login?useCookies=false", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult<InteractiveLoginResponseDto>> IAuthApi.LogInInteractive(LoginRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(InteractiveLoginEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult> IAuthApi.LogOut(CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/logout", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TwoFactorResponseDto>> IAuthApi.ManageTwoFactor(TwoFactorRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(TwoFactorEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TwoFactorResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<AccessTokenResponseDto>> IAuthApi.Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.AuthEndpoint}/refresh", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult> IAuthApi.Register(RegisterRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(RegisterEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ResendConfirmationEmail(ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(ResendConfirmationEmailEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(ResetPasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ManageInfoResponseDto>> IAuthApi.UpdateManageInfo(ManageInfoRequestDto request, CancellationToken cancellationToken)
  {
    return await ExecuteApiCall(async () =>
    {
      using var response = await _client.PostAsJsonAsync(ManageInfoEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ManageInfoResponseDto>(cancellationToken);
    });
  }

}
