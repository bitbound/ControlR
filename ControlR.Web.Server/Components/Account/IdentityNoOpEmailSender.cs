using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ControlR.Web.Server.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<AppUser>
{
  private readonly IEmailSender _emailSender = new NoOpEmailSender();

  public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
  {
    return _emailSender.SendEmailAsync(email, "Confirm your email",
      $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
  }

  public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
  {
    return _emailSender.SendEmailAsync(email, "Reset your password",
      $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");
  }

  public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
  {
    return _emailSender.SendEmailAsync(email, "Reset your password",
      $"Please reset your password using the following code: {resetCode}");
  }
}