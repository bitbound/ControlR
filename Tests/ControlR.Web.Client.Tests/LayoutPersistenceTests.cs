using Bunit;
using Bunit.TestDoubles;
using ControlR.Web.Client.Components.Layout;
using ControlR.Web.Client.StateManagement;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins the persisted component state that seeds <c>IsMainUiDisabled</c> in WASM before the
/// first render, so gated pages are never instantiated during hydration. This replaces the
/// check that previously decoded Blazor's persistence wire format from an SSR response.
/// The server integration tests in DisableMainUiTests still own redirect, not-found, and API
/// behavior.
/// </summary>
public class LayoutPersistenceTests
{
  [Fact]
  public void PersistLayoutState_WritesMainUiDisabledFalse_WhenGateNotCascaded()
  {
    using var ctx = new BunitContext();
    var persistedState = CreateServerRenderContext(ctx);

    ctx.Render<TestLayout>();
    persistedState.TriggerOnPersisting();

    Assert.True(persistedState.TryTake<bool>(PersistentStateKeys.MainUiDisabled, out var disabled));
    Assert.False(disabled);
  }

  [Fact]
  public void PersistLayoutState_WritesMainUiDisabledTrue_WhenServerCascadesDisabled()
  {
    using var ctx = new BunitContext();
    var persistedState = CreateServerRenderContext(ctx);

    ctx.Render<TestLayout>(parameters => parameters.AddCascadingValue("MainUiDisabled", true));
    persistedState.TriggerOnPersisting();

    Assert.True(persistedState.TryTake<bool>(PersistentStateKeys.MainUiDisabled, out var disabled));
    Assert.True(disabled);
  }

  /// <summary>
  /// Configures the context as a non-interactive (server-rendering) renderer and registers every
  /// service BaseLayout injects. Client-only services are unavailable, matching prerender, where
  /// the lazy injectors report Exists false and the layout reads the gate from cascading values
  /// instead.
  /// </summary>
  private static BunitPersistentComponentState CreateServerRenderContext(BunitContext ctx)
  {
    LayoutTestHost.AddBaseLayoutServices(ctx);
    var persistedState = ctx.AddBunitPersistentComponentState();
    ctx.SetRendererInfo(new RendererInfo("ssr", isInteractive: false));
    return persistedState;
  }

  /// <summary>
  /// Concrete stand-in for the abstract layout base so the test exercises the real
  /// PersistLayoutState path without the MainLayout markup and its MudBlazor component graph.
  /// </summary>
  private sealed class TestLayout : BaseLayout
  {
  }
}
