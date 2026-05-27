using System.Text;
using System.Text.Encodings.Web;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Services;

public interface IPasswordManager
{
  /// <summary>
  /// Resets another user's password from an administrator-managed flow and returns a newly generated temporary password.
  /// This is used by tenant administrators from user management screens, not by the end user performing a self-service reset.
  /// </summary>
  /// <param name="tenantId">The tenant that must own the target user.</param>
  /// <param name="targetUserId">The user whose password should be reset.</param>
  /// <returns>
  /// A result containing the generated temporary password when the reset succeeds.
  /// The target user is marked as requiring a password change on next sign-in.
  /// </returns>
  Task<Result<AdminResetPasswordResponseDto>> AdminResetPassword(Guid tenantId, Guid targetUserId);

  /// <summary>
  /// Changes the password for a currently authenticated user who knows their existing password.
  /// This is the in-session self-service flow used after the caller has already identified the target user.
  /// </summary>
  /// <param name="user">The user whose password is being changed.</param>
  /// <param name="request">The current-password and new-password payload.</param>
  /// <returns>A result indicating whether the password change succeeded.</returns>
  Task<Result> ChangePassword(AppUser user, ChangePasswordRequestDto request);

  /// <summary>
  /// Completes an end-user password reset by applying a reset token or reset-link code that was issued earlier.
  /// This is the self-service "forgot password" completion flow reached from the email link, not the admin reset flow.
  /// </summary>
  /// <param name="request">The email, reset code, and new password payload.</param>
  /// <returns>A result indicating whether the reset token was accepted and the new password was applied.</returns>
  Task<Result> CompletePasswordReset(ResetPasswordRequestDto request);

  /// <summary>
  /// Initiates a forgot-password flow for an end user who cannot sign in and needs to reset their password via email.
  /// This is the start of the self-service flow reached from the login screen, not the administrator reset flow.
  /// </summary> <param name="request">The email address payload.</param>
  /// <param name="resetPasswordUrl">The URL to include in the password reset email that the user can click to reach the password reset page. The reset code will be appended as a query parameter.</param>
  /// <returns>A result indicating whether the forgot-password email was sent. Always returns success to avoid leaking user existence information, but may fail if email sending is enabled and an error occurs during sending.</returns>
  Task<Result> ForgotPassword(ForgotPasswordRequestDto request, string resetPasswordUrl);
}

public class PasswordManager(
  AppDb appDb,
  UserManager<AppUser> userManager,
  IEmailSender<AppUser> emailSender,
  IOptions<IdentityOptions> identityOptions,
  IOptionsMonitor<AppOptions> appOptions) : IPasswordManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IEmailSender<AppUser> _emailSender = emailSender;
  private readonly IOptions<IdentityOptions> _identityOptions = identityOptions;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<Result<AdminResetPasswordResponseDto>> AdminResetPassword(Guid tenantId, Guid targetUserId)
  {
    var targetUser = await _appDb.Users.FirstOrDefaultAsync(user => 
      user.Id == targetUserId && user.TenantId == tenantId);

    if (targetUser is null)
    {
      return Result.Fail<AdminResetPasswordResponseDto>("User not found.");
    }

    var passwordOptions = _identityOptions.Value.Password;
    var minimumLength =
      (passwordOptions.RequireUppercase ? 1 : 0) +
      (passwordOptions.RequireLowercase ? 1 : 0) +
      (passwordOptions.RequireDigit ? 1 : 0) +
      (passwordOptions.RequireNonAlphanumeric ? 1 : 0);

    var temporaryPassword = RandomGenerator.GeneratePassword(
      length: Math.Max(passwordOptions.RequiredLength, minimumLength),
      includeUppercase: passwordOptions.RequireUppercase,
      includeLowercase: passwordOptions.RequireLowercase,
      includeDigits: passwordOptions.RequireDigit,
      includeSpecialChars: passwordOptions.RequireNonAlphanumeric);

    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
    var resetResult = await _userManager.ResetPasswordAsync(targetUser, resetToken, temporaryPassword);
    if (!resetResult.Succeeded)
    {
      return Result.Fail<AdminResetPasswordResponseDto>(JoinErrors(resetResult.Errors));
    }

    await SetRequirePasswordChange(targetUser.Id, true);

    return Result.Ok(new AdminResetPasswordResponseDto(temporaryPassword));
  }

  public async Task<Result> ChangePassword(AppUser user, ChangePasswordRequestDto request)
  {
    var changeResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!changeResult.Succeeded)
    {
      return Result.Fail(JoinErrors(changeResult.Errors));
    }

    await SetRequirePasswordChange(user.Id, false);

    return Result.Ok();
  }

  public async Task<Result> CompletePasswordReset(ResetPasswordRequestDto request)
  {
    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
      return Result.Ok();
    }

    var resetCode = request.ResetCode;
    if (TryDecodeResetCode(request.ResetCode, out var decodedResetCode))
    {
      resetCode = decodedResetCode;
    }

    var resetResult = await _userManager.ResetPasswordAsync(user, resetCode, request.NewPassword);
    if (!resetResult.Succeeded &&
        !string.Equals(resetCode, request.ResetCode, StringComparison.Ordinal))
    {
      resetResult = await _userManager.ResetPasswordAsync(user, request.ResetCode, request.NewPassword);
    }

    if (!resetResult.Succeeded)
    {
      return Result.Fail(JoinErrors(resetResult.Errors));
    }

    await SetRequirePasswordChange(user.Id, false);

    return Result.Ok();
  }

  public async Task<Result> ForgotPassword(ForgotPasswordRequestDto request, string resetPasswordUrl)
  {
    if (_appOptions.CurrentValue.DisableEmailSending)
    {
      return Result.Ok();
    }

    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
    {
      return Result.Ok();
    }

    var code = await _userManager.GeneratePasswordResetTokenAsync(user);
    var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
    var callbackUrl = QueryHelpers.AddQueryString(resetPasswordUrl, "code", encodedCode);

    await _emailSender.SendPasswordResetLinkAsync(user, request.Email, HtmlEncoder.Default.Encode(callbackUrl));

    return Result.Ok();
  }

  private static string JoinErrors(IEnumerable<IdentityError> errors)
  {
    return string.Join(", ", errors.Select(error => error.Description));
  }

  private static bool TryDecodeResetCode(string resetCode, out string decodedResetCode)
  {
    try
    {
      decodedResetCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetCode));
      return true;
    }
    catch (FormatException)
    {
      decodedResetCode = string.Empty;
      return false;
    }
  }

  private async Task SetRequirePasswordChange(Guid userId, bool requirePasswordChange)
  {
    var user = await _appDb.Users.FirstOrDefaultAsync(appUser => appUser.Id == userId);
    if (user is null || user.RequirePasswordChange == requirePasswordChange)
    {
      return;
    }

    user.RequirePasswordChange = requirePasswordChange;
    await _appDb.SaveChangesAsync();
  }
}
