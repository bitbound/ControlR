using Bitbound.SimpleMessenger;
using Bunit;
using ControlR.Libraries.Shared.Services;
using ControlR.Web.Client.Components.Layout;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Security.Claims;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Registers the services a layout needs for a non-interactive (prerender-shaped) bUnit render,
/// where the client-only services are missing from the scope and the lazy injectors report
/// Exists false.
/// </summary>
internal static class LayoutTestHost
{
  /// <summary>
  /// Registers the dependency set BaseLayout injects. bUnit freezes its service provider at the
  /// first retrieval, and both SetRendererInfo and AddBunitPersistentComponentState retrieve
  /// services, so every registration has to happen before either call.
  /// </summary>
  public static void AddBaseLayoutServices(BunitContext ctx)
  {
    ctx.Services.AddSingleton<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();
    ctx.Services.AddSingleton<ILogger<BaseLayout>>(NullLogger<BaseLayout>.Instance);
    ctx.Services.AddSingleton<ILazyInjector<IJsInterop>>(new NotInjected<IJsInterop>());
    ctx.Services.AddSingleton<ILazyInjector<IMessenger>>(new NotInjected<IMessenger>());
    ctx.Services.AddSingleton<ILazyInjector<IPersistentStateAccessor>>(new NotInjected<IPersistentStateAccessor>());
    ctx.Services.AddSingleton<ILazyInjector<ISnackbar>>(new NotInjected<ISnackbar>());
    ctx.Services.AddSingleton<ILazyInjector<IThemeStateProvider>>(new NotInjected<IThemeStateProvider>());
    ctx.Services.AddSingleton(new Mock<IUserPreferencesProvider>().Object);
  }

  /// <summary>
  /// Adds what MainLayout's subtree injects on top of <see cref="AddBaseLayoutServices"/>. The
  /// MudBlazor providers under ControlRLayoutProvider need their services registered, and NotFound
  /// subscribes to the browser viewport through its base component.
  /// </summary>
  public static void AddMainLayoutServices(BunitContext ctx)
  {
    AddBaseLayoutServices(ctx);

    ctx.Services.AddMudServices();
    ctx.Services.AddSingleton<ILazyInjector<IAppEnvironment>>(new NotInjected<IAppEnvironment>());
    ctx.Services.AddSingleton<ILazyInjector<IHubConnector>>(new NotInjected<IHubConnector>());
    ctx.Services.AddSingleton<ILazyInjector<IWhatsNewNotifier>>(new NotInjected<IWhatsNewNotifier>());
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
}
