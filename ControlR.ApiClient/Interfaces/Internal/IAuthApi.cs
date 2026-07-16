using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IAuthApi
{
  [ApiRoute("POST", "/api/internal/auth/change-password")]
  Task<ApiResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/change-password-with-credentials")]
  Task<ApiResult> ChangePasswordWithCredentials(CredentialPasswordChangeRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/complete-password-reset")]
  Task<ApiResult> CompletePasswordReset(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/auth/confirmEmail")]
  Task<ApiResult<string>> ConfirmEmail(Guid userId, string code, string? changedEmail = null, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/forgotPassword")]
  Task<ApiResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/auth/me")]
  Task<ApiResult<CurrentUserResponseDto>> GetCurrentUser(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/auth/manage/info")]
  Task<ApiResult<ManageInfoResponseDto>> GetManageInfo(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/login")]
  Task<ApiResult<AccessTokenResponseDto>> LogIn(LoginRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/interactive-login")]
  Task<ApiResult<InteractiveLoginResponseDto>> LogInInteractive(LoginRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/logout")]
  Task<ApiResult> LogOut(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/manage/2fa")]
  Task<ApiResult<TwoFactorResponseDto>> ManageTwoFactor(TwoFactorRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/refresh")]
  Task<ApiResult<AccessTokenResponseDto>> Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/register")]
  Task<ApiResult> Register(RegisterRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/resendConfirmationEmail")]
  Task<ApiResult> ResendConfirmationEmail(ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/resetPassword")]
  Task<ApiResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/auth/manage/info")]
  Task<ApiResult<ManageInfoResponseDto>> UpdateManageInfo(ManageInfoRequestDto request, CancellationToken cancellationToken = default);
}
