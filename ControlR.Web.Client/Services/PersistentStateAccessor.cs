using ControlR.Web.Client.StateManagement;

namespace ControlR.Web.Client.Services;

public interface IPersistentStateAccessor
{
  ThemeMode DefaultThemeMode { get; }
  bool IsDarkMode { get; }
  bool IsDecommissioned { get; }
  UserInfo? UserInfo { get; }
}

public sealed class PersistentStateAccessor : IPersistentStateAccessor, IDisposable
{
  private const string DarkModeKey = PersistentStateKeys.IsDarkMode;
  private const string DecommissionStateKey = PersistentStateKeys.ServerDecommissioned;
  private const string DefaultThemeModeKey = PersistentStateKeys.DefaultThemeMode;
  private const string UserInfoKey = PersistentStateKeys.UserInfo;

  private readonly ILogger<PersistentStateAccessor> _logger;
  private readonly PersistingComponentStateSubscription _persistingSubscription;

  private bool _isDisposed;

  public PersistentStateAccessor(
    PersistentComponentState state,
    ILogger<PersistentStateAccessor> logger)
  {
    _logger = logger;

    if (state.TryTakeFromJson<ThemeMode>(DefaultThemeModeKey, out var defaultThemeMode))
    {
      _logger.LogDebug("Loaded persisted default theme mode: {DefaultThemeMode}.", defaultThemeMode);
      DefaultThemeMode = defaultThemeMode;
    }

    // Consumed by BaseLayout to seed IsDarkMode synchronously before the first await
    // in OnInitializedAsync, preventing a dark flash during SSR→WASM hydration.
    // DO NOT REMOVE — see the early-set in BaseLayout.OnInitializedAsync.
    if (state.TryTakeFromJson<bool>(DarkModeKey, out var isDarkMode))
    {
      _logger.LogDebug("Loaded persisted dark mode state: {IsDarkMode}.", isDarkMode);
      IsDarkMode = isDarkMode;
    }

    if (state.TryTakeFromJson<bool>(DecommissionStateKey, out var isDecommissioned))
    {
      _logger.LogDebug("Loaded persisted decommission state: {IsDecommissioned}.", isDecommissioned);
      IsDecommissioned = isDecommissioned;
    }

    if (state.TryTakeFromJson<UserInfo>(UserInfoKey, out var userInfo) && userInfo is not null)
    {
      _logger.LogDebug("Loaded persisted user info for: {Email}.", userInfo.Email);
      UserInfo = userInfo;
    }

    _persistingSubscription = state.RegisterOnPersisting(() =>
      {
        state.PersistAsJson(DefaultThemeModeKey, DefaultThemeMode);
        state.PersistAsJson(DecommissionStateKey, IsDecommissioned);
        return Task.CompletedTask;
      });
  }

  public ThemeMode DefaultThemeMode { get; private set; }
  public bool IsDarkMode { get; private set; }
  public bool IsDecommissioned { get; private set; }
  public UserInfo? UserInfo { get; private set; }

  public void Dispose()
  {
    if (_isDisposed) return;
    _isDisposed = true;
    _persistingSubscription.Dispose();
    GC.SuppressFinalize(this);
  }
}
