using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IAuthApi
{
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/change-password", "POST")]
  Task<ApiResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/change-password-with-credentials", "POST")]
  Task<ApiResult> ChangePasswordWithCredentials(CredentialPasswordChangeRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/complete-password-reset", "POST")]
  Task<ApiResult> CompletePasswordReset(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/confirmEmail", "GET")]
  Task<ApiResult<string>> ConfirmEmail(Guid userId, string code, string? changedEmail = null, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/forgotPassword", "POST")]
  Task<ApiResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/me", "GET")]
  Task<ApiResult<CurrentUserResponseDto>> GetCurrentUser(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/manage/info", "GET")]
  Task<ApiResult<ManageInfoResponseDto>> GetManageInfo(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/login", "POST")]
  Task<ApiResult<AccessTokenResponseDto>> LogIn(LoginRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/interactive-login", "POST")]
  Task<ApiResult<InteractiveLoginResponseDto>> LogInInteractive(LoginRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/logout", "POST")]
  Task<ApiResult> LogOut(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/manage/2fa", "POST")]
  Task<ApiResult<TwoFactorResponseDto>> ManageTwoFactor(TwoFactorRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/refresh", "POST")]
  Task<ApiResult<AccessTokenResponseDto>> Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/register", "POST")]
  Task<ApiResult> Register(RegisterRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/resendConfirmationEmail", "POST")]
  Task<ApiResult> ResendConfirmationEmail(ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/resetPassword", "POST")]
  Task<ApiResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.AuthEndpoint}/manage/info", "POST")]
  Task<ApiResult<ManageInfoResponseDto>> UpdateManageInfo(ManageInfoRequestDto request, CancellationToken cancellationToken = default);
}
