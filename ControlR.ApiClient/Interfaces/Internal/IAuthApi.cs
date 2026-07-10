using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IAuthApi
{
  Task<ApiResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> ChangePasswordWithCredentials(CredentialPasswordChangeRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> CompletePasswordReset(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<string>> ConfirmEmail(Guid userId, string code, string? changedEmail = null, CancellationToken cancellationToken = default);
  Task<ApiResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<CurrentUserResponseDto>> GetCurrentUser(CancellationToken cancellationToken = default);
  Task<ApiResult<ManageInfoResponseDto>> GetManageInfo(CancellationToken cancellationToken = default);
  Task<ApiResult<AccessTokenResponseDto>> LogIn(LoginRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<InteractiveLoginResponseDto>> LogInInteractive(LoginRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> LogOut(CancellationToken cancellationToken = default);
  Task<ApiResult<TwoFactorResponseDto>> ManageTwoFactor(TwoFactorRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<AccessTokenResponseDto>> Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> Register(RegisterRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> ResendConfirmationEmail(ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<ManageInfoResponseDto>> UpdateManageInfo(ManageInfoRequestDto request, CancellationToken cancellationToken = default);
}
