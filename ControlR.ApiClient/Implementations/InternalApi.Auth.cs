using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.ApiClient;

internal partial class InternalApi
{
  private const string ChangePasswordEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/change-password";
  private const string ChangePasswordWithCredentialsEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/change-password-with-credentials";
  private const string CompletePasswordResetEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/complete-password-reset";
  private const string ConfirmEmailEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/confirmEmail";
  private const string CurrentUserEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/me";
  private const string ForgotPasswordEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/forgotPassword";
  private const string InteractiveLoginEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/interactive-login";
  private const string ManageInfoEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/manage/info";
  private const string RegisterEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/register";
  private const string ResendConfirmationEmailEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/resendConfirmationEmail";
  private const string ResetPasswordEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/resetPassword";
  private const string TwoFactorEndpoint = $"{HttpConstants.Internal.AuthEndpoint}/manage/2fa";

  async Task<ApiResult> IAuthApi.ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ChangePasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ChangePasswordWithCredentials(CredentialPasswordChangeRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ChangePasswordWithCredentialsEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.CompletePasswordReset(ResetPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(CompletePasswordResetEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<string>> IAuthApi.ConfirmEmail(Guid userId, string code, string? changedEmail, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      var endpoint = $"{ConfirmEmailEndpoint}?userId={Uri.EscapeDataString(userId.ToString())}&code={Uri.EscapeDataString(code)}";
      if (!string.IsNullOrWhiteSpace(changedEmail))
      {
        endpoint += $"&changedEmail={Uri.EscapeDataString(changedEmail)}";
      }

      using var response = await _client.HttpClient.GetAsync(endpoint, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadAsStringAsync(cancellationToken);
    });
  }

  async Task<ApiResult> IAuthApi.ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ForgotPasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<CurrentUserResponseDto>> IAuthApi.GetCurrentUser(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(CurrentUserEndpoint, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CurrentUserResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<ManageInfoResponseDto>> IAuthApi.GetManageInfo(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.GetAsync(ManageInfoEndpoint, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ManageInfoResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<AccessTokenResponseDto>> IAuthApi.LogIn(LoginRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.AuthEndpoint}/login?useCookies=false", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult<InteractiveLoginResponseDto>> IAuthApi.LogInInteractive(LoginRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(InteractiveLoginEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<InteractiveLoginResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult> IAuthApi.LogOut(CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.AuthEndpoint}/logout", new { }, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<TwoFactorResponseDto>> IAuthApi.ManageTwoFactor(TwoFactorRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(TwoFactorEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<TwoFactorResponseDto>(cancellationToken);
    });
  }

  async Task<ApiResult<AccessTokenResponseDto>> IAuthApi.Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync($"{HttpConstants.Internal.AuthEndpoint}/refresh", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken);
    }, allowAutoRefresh: false);
  }

  async Task<ApiResult> IAuthApi.Register(RegisterRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(RegisterEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ResendConfirmationEmail(ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ResendConfirmationEmailEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult> IAuthApi.ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ResetPasswordEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<ManageInfoResponseDto>> IAuthApi.UpdateManageInfo(ManageInfoRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(ManageInfoEndpoint, request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<ManageInfoResponseDto>(cancellationToken);
    });
  }

}
