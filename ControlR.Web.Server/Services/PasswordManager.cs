using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Services;

public interface IPasswordManager
{
  Task<Result<AdminResetPasswordResponseDto>> AdminResetPassword(Guid tenantId, Guid targetUserId);
  Task<Result> ChangePassword(AppUser user, ChangePasswordRequestDto request);
  Task<Result> ForgotPassword(ForgotPasswordRequestDto request, string resetPasswordUrl);
  Task<Result> ResetPassword(ResetPasswordRequestDto request);
}

public class PasswordManager(
  AppDb appDb,
  UserManager<AppUser> userManager,
  IEmailSender<AppUser> emailSender,
  IOptionsMonitor<AppOptions> appOptions) : IPasswordManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IEmailSender<AppUser> _emailSender = emailSender;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<Result<AdminResetPasswordResponseDto>> AdminResetPassword(Guid tenantId, Guid targetUserId)
  {
    var targetExists = await _appDb.Users
      .AsNoTracking()
      .AnyAsync(user => user.Id == targetUserId && user.TenantId == tenantId);

    if (!targetExists)
    {
      return Result.Fail<AdminResetPasswordResponseDto>("User not found");
    }

    var targetUser = await _userManager.FindByIdAsync(targetUserId.ToString());
    if (targetUser is null)
    {
      return Result.Fail<AdminResetPasswordResponseDto>("User not found");
    }

    var temporaryPassword = ControlR.Libraries.Shared.Helpers.RandomGenerator.CreateAccessToken()[..16];
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

  public async Task<Result> ResetPassword(ResetPasswordRequestDto request)
  {
    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
      return Result.Ok();
    }

    var resetResult = await _userManager.ResetPasswordAsync(user, request.ResetCode, request.NewPassword);
    if (!resetResult.Succeeded)
    {
      return Result.Fail(JoinErrors(resetResult.Errors));
    }

    await SetRequirePasswordChange(user.Id, false);

    return Result.Ok();
  }

  private static string JoinErrors(IEnumerable<IdentityError> errors)
  {
    return string.Join(", ", errors.Select(error => error.Description));
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