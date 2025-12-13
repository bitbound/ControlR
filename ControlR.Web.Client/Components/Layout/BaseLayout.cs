using ControlR.Web.Client.StateManagement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Layout;

public abstract class BaseLayout : LayoutComponentBase, IAsyncDisposable
{
  private MudTheme? _customTheme;

  [Inject]
  public required PersistentComponentState ApplicationState { get; set; }
  [Inject]
  public required AuthenticationStateProvider AuthState { get; set; }
  [Inject]
  public required ILogger<BaseLayout> BaseLogger { get; set; }
  [Inject]
  public required ILazyInjector<IJsInterop> JsInterop { get; set; }
  [Inject]
  public required ILazyInjector<IMessenger> Messenger { get; set; }
  [Inject]
  public required NavigationManager NavManager { get; set; }
  [Inject]
  public required ILazyInjector<ISnackbar> Snackbar { get; set; }
  [Inject]
  public required IUserSettingsProvider UserSettings { get; set; }

  protected Palette CurrentPalette => IsDarkMode
      ? CustomTheme.PaletteDark
      : CustomTheme.PaletteLight;
  protected ThemeMode CurrentThemeMode { get; set; } = ThemeMode.Auto;
  protected MudTheme CustomTheme =>
      _customTheme ??= new MudTheme
      {
        PaletteDark = Theme.DarkPalette,
        PaletteLight = Theme.LightPalette
      };
  protected bool DrawerOpen { get; set; } = true;
  protected bool IsAuthenticated { get; set; }
  protected bool IsDarkMode { get; set; } = true;
  protected PersistingComponentStateSubscription PersistingSubscription { get; set; }
  protected string ThemeClass => IsDarkMode ? "dark-mode" : "light-mode";

  public virtual ValueTask DisposeAsync()
  {
    try
    {
      PersistingSubscription.Dispose();
      if (RendererInfo.IsInteractive)
      {
        Messenger.Value.UnregisterAll(this);
      }
    }
    catch (Exception ex)
    {
      BaseLogger.LogError(ex, "Error during BaseLayout disposal.");
    }

    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
  }

  protected async Task<bool> GetSystemDarkMode()
  {
    try
    {
      if (RendererInfo.IsInteractive)
      {
        return await JsInterop.Value.GetSystemDarkMode();
      }
      return true; // Default to dark during prerendering
    }
    catch (Exception ex)
    {
      BaseLogger.LogWarning(ex, "Failed to get system dark mode preference. Defaulting to dark.");
      return true;
    }
  }
  protected virtual async Task HandleThemeChanged(ThemeMode mode)
  {
    CurrentThemeMode = mode;
    await UpdateIsDarkMode();
    StateHasChanged();
  }
  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var authState = await AuthState.GetAuthenticationStateAsync();
    IsAuthenticated = authState.User.Identity?.IsAuthenticated ?? false;

    // Try to restore persisted state from SSR
    if (!ApplicationState.TryTakeFromJson<bool>(PersistentStateKeys.IsDarkMode, out var persistedIsDarkMode))
    {
      // No persisted state, this is SSR or first load
      if (IsAuthenticated)
      {
        CurrentThemeMode = await UserSettings.GetThemeMode();
      }
      await UpdateIsDarkMode();

      // Register a callback to persist state before SSR completes
      PersistingSubscription = ApplicationState.RegisterOnPersisting(PersistThemeState);
    }
    else
    {
      // Restored from persisted state (this is WASM after SSR)
      IsDarkMode = persistedIsDarkMode;

      // Still need to load theme mode
      if (IsAuthenticated)
      {
        CurrentThemeMode = await UserSettings.GetThemeMode();
      }
    }

    if (RendererInfo.IsInteractive)
    {
      Messenger.Value.Register<ToastMessage>(this, HandleToastMessage);
      Messenger.Value.Register<ThemeChangedMessage>(this, HandleThemeChangedMessage);
    }
  }
  protected Task PersistThemeState()
  {
    ApplicationState.PersistAsJson(PersistentStateKeys.IsDarkMode, IsDarkMode);
    return Task.CompletedTask;
  }
  protected void ToggleNavDrawer()
  {
    DrawerOpen = !DrawerOpen;
  }
  protected virtual async Task UpdateIsDarkMode()
  {
    IsDarkMode = CurrentThemeMode switch
    {
      ThemeMode.Light => false,
      ThemeMode.Dark => true,
      ThemeMode.Auto => await GetSystemDarkMode(),
      _ => true
    };
  }

  private async Task HandleThemeChangedMessage(object subscriber, ThemeChangedMessage message)
  {
    await HandleThemeChanged(message.ThemeMode);
  }
  private Task HandleToastMessage(object subscriber, ToastMessage toast)
  {
    Snackbar.Value.Add(toast.Message, toast.Severity);
    return Task.CompletedTask;
  }
}
