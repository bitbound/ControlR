using Microsoft.Playwright;

namespace ControlR.Web.Server.UITests;

public class DashboardLoginTests
{
  [Fact(Timeout = 180_000)]
  public async Task Login_WithBootstrapAdmin_NavigatesToDashboard()
  {
    await using var session = await UiBrowserSession.Create();
    var page = session.Page;

    // Navigate to login page
    await page.GotoAsync($"{session.Options.BaseUrl}Account/Login");

    // Fill credentials using MudBlazor placeholder-based selectors
    await page.GetByPlaceholder("name@example.com").FillAsync(UiTestConstants.BootstrapAdminEmail);
    await page.GetByPlaceholder("password").FillAsync(UiTestConstants.BootstrapAdminPassword);

    // Submit — use substring match to handle possible whitespace normalization
    await page.GetByRole(AriaRole.Button, new() { Name = "Log" }).ClickAsync();

    // Wait for the page to finish loading after the redirect
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    // Assert we're no longer on the login page
    Assert.DoesNotContain("/Account/Login", page.Url);

    // Save screenshot for debugging
    await session.SaveScreenshot(nameof(DashboardLoginTests) + "_" + nameof(Login_WithBootstrapAdmin_NavigatesToDashboard));
  }
}
