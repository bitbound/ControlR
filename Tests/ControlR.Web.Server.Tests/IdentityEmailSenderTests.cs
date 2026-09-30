using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Pins that <see cref="IdentityEmailSender"/> rebuilds each link's origin from the configured public
/// base URL before sending.
/// </summary>
/// <remarks>
/// <c>MapIdentityApi</c> builds its confirmation links from the arriving request's scheme and host, so
/// this seam is what stops a forged <c>Host</c> from putting a genuine token-bearing link in front of a
/// victim. End-to-end coverage lives in <c>ForgedForwardedHostTests</c>.
/// </remarks>
public class IdentityEmailSenderTests
{
  private const string ConfiguredOrigin = "https://controlr.test";
  private const string ForgedOrigin = "http://evil.example.com";
  private const string Recipient = "victim@t.local";

  [Fact]
  public async Task SendConfirmationLinkAsync_DoesNotSend_WhenNoOriginIsConfigured()
  {
    var (sender, emailSender) = CreateSender(baseUrl: null);

    await sender.SendConfirmationLinkAsync(
      new AppUser(), Recipient, $"{ForgedOrigin}/api/auth/confirmEmail?userId=1&amp;code=abc");

    emailSender.Verify(
      x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
      Times.Never);
  }

  [Fact]
  public async Task SendConfirmationLinkAsync_KeepsALinkThatAlreadyUsesTheConfiguredOrigin()
  {
    var (sender, emailSender) = CreateSender(ConfiguredOrigin);
    var link = $"{ConfiguredOrigin}/Account/ConfirmEmail?userId=42&amp;code=abc";

    await sender.SendConfirmationLinkAsync(new AppUser(), Recipient, link);

    var body = CaptureBody(emailSender);
    Assert.Contains(link, body, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SendConfirmationLinkAsync_RewritesAForgedOriginToTheConfiguredOne()
  {
    var (sender, emailSender) = CreateSender(ConfiguredOrigin);

    await sender.SendConfirmationLinkAsync(
      new AppUser(), Recipient, $"{ForgedOrigin}/api/auth/confirmEmail?userId=1&amp;code=abc");

    var body = CaptureBody(emailSender);
    Assert.DoesNotContain("evil.example.com", body, StringComparison.OrdinalIgnoreCase);
    Assert.Contains(
      $"{ConfiguredOrigin}/api/auth/confirmEmail?userId=1&amp;code=abc",
      body,
      StringComparison.Ordinal);
  }

  [Fact]
  public async Task SendPasswordResetCodeAsync_SendsTheBareCode_WithoutAnOrigin()
  {
    var (sender, emailSender) = CreateSender(ConfiguredOrigin);

    await sender.SendPasswordResetCodeAsync(new AppUser(), Recipient, "123456");

    var body = CaptureBody(emailSender);
    Assert.Contains("123456", body, StringComparison.Ordinal);
    Assert.DoesNotContain("http", body, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task SendPasswordResetLinkAsync_RewritesAForgedOriginToTheConfiguredOne()
  {
    var (sender, emailSender) = CreateSender(ConfiguredOrigin);

    await sender.SendPasswordResetLinkAsync(
      new AppUser(), Recipient, $"{ForgedOrigin}/Account/ResetPassword?code=abc");

    var body = CaptureBody(emailSender);
    Assert.DoesNotContain("evil.example.com", body, StringComparison.OrdinalIgnoreCase);
    Assert.Contains(
      $"{ConfiguredOrigin}/Account/ResetPassword?code=abc",
      body,
      StringComparison.Ordinal);
  }

  private static string CaptureBody(Mock<IEmailSender> emailSender)
  {
    return emailSender.Invocations
      .SelectMany(invocation => invocation.Arguments)
      .OfType<string>()
      .Last();
  }

  private static (IdentityEmailSender Sender, Mock<IEmailSender> EmailSender) CreateSender(string? baseUrl)
  {
    var emailSender = new Mock<IEmailSender>();
    var publicUrlProvider = new Mock<IPublicUrlProvider>();
    publicUrlProvider.Setup(x => x.TryGetBaseUrl()).Returns(baseUrl);

    var sender = new IdentityEmailSender(
      emailSender.Object,
      NullLogger<IdentityEmailSender>.Instance,
      publicUrlProvider.Object);

    return (sender, emailSender);
  }
}
