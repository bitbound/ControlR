using Bitbound.SimpleMessenger;
using Bunit;
using Bunit.TestDoubles;
using ControlR.Libraries.Shared.Services;
using ControlR.Web.Client.Components.Layout;
using ControlR.Web.Client.Services;
using ControlR.Web.Client.StateManagement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using System.Security.Claims;

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
    // bUnit freezes its service provider at the first retrieval, and both the fake
    // registration and SetRendererInfo retrieve services, so every registration must happen up
    // front. The fake drives persistence through the PersistentComponentState the components
    // receive at injection.
    ctx.Services.AddSingleton<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();
    ctx.Services.AddSingleton<ILogger<BaseLayout>>(NullLogger<BaseLayout>.Instance);
    ctx.Services.AddSingleton<ILazyInjector<IJsInterop>>(new NotInjected<IJsInterop>());
    ctx.Services.AddSingleton<ILazyInjector<IMessenger>>(new NotInjected<IMessenger>());
    ctx.Services.AddSingleton<ILazyInjector<IPersistentStateAccessor>>(new NotInjected<IPersistentStateAccessor>());
    ctx.Services.AddSingleton<ILazyInjector<ISnackbar>>(new NotInjected<ISnackbar>());
    ctx.Services.AddSingleton<ILazyInjector<IThemeStateProvider>>(new NotInjected<IThemeStateProvider>());
    ctx.Services.AddSingleton(new Mock<IUserPreferencesProvider>().Object);
    var persistedState = ctx.AddBunitPersistentComponentState();
    ctx.SetRendererInfo(new RendererInfo("ssr", isInteractive: false));
    return persistedState;
  }

  private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
  {
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
      return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
  }

  /// <summary>
  /// Mirrors prerender, where client-side services are missing from the server's DI scope. Any
  /// read of Value is a test failure by construction, because a non-interactive render must not
  /// touch those services.
  /// </summary>
  private sealed class NotInjected<T> : ILazyInjector<T> where T : class
  {
    public bool Exists => false;

    public T? Maybe => null;

    public T Value => throw new InvalidOperationException(
      $"A non-interactive render resolved {typeof(T).Name} through ILazyInjector.Value.");
  }

  /// <summary>
  /// Concrete stand-in for the abstract layout base so the test exercises the real
  /// PersistLayoutState path without the MainLayout markup and its MudBlazor component graph.
  /// </summary>
  private sealed class TestLayout : BaseLayout
  {
  }
}
