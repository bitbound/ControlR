using Microsoft.Playwright;

namespace ControlR.Web.Server.UITests;

public class HomePageSmokeTests
{
  [Fact(Timeout = 180_000)]
  public async Task WelcomePage_ShouldRender_AndNavigateToLogin()
  {
    await using var session = await UiBrowserSession.Create();

    await session.Page.GotoAsync(session.Options.BaseUrl.ToString(), new PageGotoOptions
    {
      WaitUntil = WaitUntilState.DOMContentLoaded,
    });

    var appTitle = session.Page.GetByText("ControlR", new PageGetByTextOptions
    {
      Exact = true,
    });

    await appTitle.WaitForAsync();

    var loginLink = session.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
    {
      Name = "Login",
      Exact = true,
    });

    await loginLink.ClickAsync();
    await session.Page.WaitForURLAsync("**/Account/Login*");

    var loginHeading = session.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions
    {
      Name = "Log in",
      Exact = true,
    });

    await loginHeading.WaitForAsync();

    await session.SaveScreenshot("login-page");
  }
}