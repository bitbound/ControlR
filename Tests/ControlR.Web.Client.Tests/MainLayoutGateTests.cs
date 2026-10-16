using Bunit;
using ControlR.Web.Client.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins what MainLayout renders while the server's DisableMainUi option is on. The gate replaces
/// the whole page, chrome included, with the not-found view, so a gated page body is never
/// instantiated.
/// </summary>
public class MainLayoutGateTests
{
  [Fact]
  public async Task RendersNotFoundViewWithoutChrome_WhenMainUiDisabled()
  {
    await using var ctx = new BunitContext();
    LayoutTestHost.AddMainLayoutServices(ctx);

    ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    ctx.AddBunitPersistentComponentState();
    ctx.SetRendererInfo(new RendererInfo("ssr", isInteractive: false));

    // The body stands in for whatever page the router asked for, so the assertions below can tell
    // whether it was rendered at all.
    var cut = ctx.Render<MainLayout>(parameters => parameters
      .AddCascadingValue("MainUiDisabled", true)
      .Add(p => p.Body, builder => builder.AddMarkupContent(0, "<div id='gated-body-marker'></div>")));

    Assert.NotEmpty(cut.FindAll(".not-found-container"));
    Assert.Empty(cut.FindAll("#gated-body-marker"));

    // The app chrome goes with the body.
    Assert.Empty(cut.FindAll(".mud-appbar"));
    Assert.Empty(cut.FindAll(".mud-navmenu"));

    // The gate passes HideActionButtons, so the view's recovery links stay out too.
    Assert.Empty(cut.FindAll(".action-button"));
  }
}
