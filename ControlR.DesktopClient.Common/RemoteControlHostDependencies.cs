using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Avalonia.Services;
using ControlR.Libraries.Shared.Services.Locks;

namespace ControlR.DesktopClient.Common;

/// <summary>
/// The app-owned services a remote control session host takes from the application starting it.
/// </summary>
/// <remarks>
/// A member every app can supply is required, so adding one breaks all four call sites at compile
/// time. A member only some platforms have is nullable, so the platforms without it are not forced
/// to invent one; <see cref="ServiceRegistrationExtensions.ConfigureRemoteControlHostValidation"/>
/// turns a forgotten forwarding into a build-time failure.
/// </remarks>
public sealed record RemoteControlHostDependencies
{
  public required IToaster Toaster { get; init; }

  public required IUserInteractionService UserInteraction { get; init; }

  public required IRemoteControlDtoSender DtoSender { get; init; }

  public IUiDispatcher? Dispatcher { get; init; }

  public IAsyncLock? AsyncLock { get; init; }

  public ISessionConsentService? Consent { get; init; }
}