using ControlR.Web.Client.StateManagement;
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
  public required ILazyInjector<IJsInterop> JsInterop { get; set; }
  [Inject]
  public required ILogger<BaseLayout> Logger { get; set; }
  [Inject]
  public required ILazyInjector<IMessenger> Messenger { get; set; }
  [Inject]
  public required NavigationManager NavManager { get; set; }
  [Inject]
  public required ILazyInjector<IPersistentStateAccessor> PersistentState { get; set; }
  [Inject]
  public required ILazyInjector<ISnackbar> Snackbar { get; set; }
  [Inject]
  public required ILazyInjector<IThemeStateProvider> ThemeState { get; set; }
  [Inject]
  public required IUserPreferencesProvider UserPreferences { get; set; }

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
  [CascadingParameter(Name = "AcceptsInteractiveRouting")]
  protected bool IsInteractiveRoutingPage { get; set; }
  protected PersistingComponentStateSubscription PersistingSubscription { get; set; }
  [CascadingParameter(Name = "DefaultThemeMode")]
  protected ThemeMode ServerDefaultThemeMode { get; set; }
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
      Logger.LogError(ex, "Error during BaseLayout disposal.");
    }

    GC.SuppressFinalize(this);
    return ValueTask.CompletedTask;
  }

  protected virtual async Task HandleThemeChanged(ThemeMode mode)
  {
    CurrentThemeMode = mode;
    ThemeState.Value.SetThemeMode(mode);
    await UpdateIsDarkMode();
    await InvokeAsync(StateHasChanged);
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    // Set IsDarkMode synchronously before the first await.  Blazor can render between
    // await yield points within OnInitializedAsync.  If IsDarkMode is still the field
    // default (true/dark) when that happens, the user sees a dark flash before the
    // correct value is applied below.  This early seed prevents it.  DO NOT REMOVE.
    if (PersistentState.Exists)
    {
      IsDarkMode = PersistentState.Value.IsDarkMode;
    }
    else
    {
      IsDarkMode = ServerDefaultThemeMode != ThemeMode.Light;
    }

    var authState = await AuthState.GetAuthenticationStateAsync();
    IsAuthenticated = authState.User.Identity?.IsAuthenticated ?? false;

    // Load the user's stored theme preference in both SSR and WASM paths.
    if (IsAuthenticated)
    {
      CurrentThemeMode = (await UserPreferences.GetPreferences()).ThemeMode;
    }
    else if (PersistentState.Exists)
    {
      // WASM: unauthenticated users always use the server's configured default.
      CurrentThemeMode = PersistentState.Value.DefaultThemeMode;
    }
    else
    {
      // SSR: cascading value from App.razor (IOptions<AppOptions>.DefaultThemeMode).
      CurrentThemeMode = ServerDefaultThemeMode;
    }

    // Evaluate IsDarkMode from the effective CurrentThemeMode.
    //   - During SSR, GetSystemDarkMode() defaults to dark (JS unavailable).
    //   - During WASM hydration, it queries the actual browser preference.
    await UpdateIsDarkMode();

    // During SSR, register a callback to persist state before the response is sent.
    if (!PersistentState.Exists)
    {
      PersistingSubscription = ApplicationState.RegisterOnPersisting(PersistThemeState);
    }

    if (RendererInfo.IsInteractive)
    {
      ThemeState.Value.SetThemeMode(CurrentThemeMode);
      ThemeState.Value.SetIsDarkMode(IsDarkMode);
      Messenger.Value.Register<MudToastMessage>(this, HandleToastMessage);
      Messenger.Value.Register<ThemeChangedMessage>(this, HandleThemeChangedMessage);
    }
  }

  protected void ToggleNavDrawer()
  {
    DrawerOpen = !DrawerOpen;
  }


  private async Task<bool> GetSystemDarkMode()
  {
    try
    {
      if (RendererInfo.IsInteractive)
      {
        return await JsInterop.Value.GetSystemDarkMode();
      }
      // During SSR, JS is unavailable. Use the server's DefaultThemeMode as the hint.
      return ServerDefaultThemeMode switch
      {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => true
      };
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Failed to get system dark mode preference. Defaulting to dark.");
      return true;
    }
  }

  private async Task HandleThemeChangedMessage(object subscriber, ThemeChangedMessage message)
  {
    await HandleThemeChanged(message.ThemeMode);
  }

  private Task HandleToastMessage(object subscriber, MudToastMessage toast)
  {
    Snackbar.Value.Add(toast.Message, toast.Severity);
    return Task.CompletedTask;
  }

  // Persists the SSR-computed IsDarkMode so WASM hydration can seed it before the
  // first await (see the early-set above).  Without this, WASM would flash dark
  // before UpdateIsDarkMode runs.  DO NOT REMOVE.
  private Task PersistThemeState()
  {
    ApplicationState.PersistAsJson(PersistentStateKeys.IsDarkMode, IsDarkMode);
    ApplicationState.PersistAsJson(PersistentStateKeys.DefaultThemeMode, ServerDefaultThemeMode);
    return Task.CompletedTask;
  }

  private async Task UpdateIsDarkMode()
  {
    IsDarkMode = CurrentThemeMode switch
    {
      ThemeMode.Light => false,
      ThemeMode.Dark => true,
      ThemeMode.Auto => await GetSystemDarkMode(),
      _ => true
    };

    if (RendererInfo.IsInteractive)
    {
      ThemeState.Value.SetIsDarkMode(IsDarkMode);
    }
  }
}
