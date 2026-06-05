using ControlR.Web.Client.StateManagement;

namespace ControlR.Web.Client.Services;

public interface IPersistentStateAccessor
{
  bool IsDarkMode { get; }
  bool IsDecommissioned { get; }
  UserInfo? UserInfo { get; }
}

public sealed class PersistentStateAccessor : IPersistentStateAccessor, IDisposable
{
  private const string DarkModeKey = PersistentStateKeys.IsDarkMode;
  private const string DecommissionStateKey = PersistentStateKeys.ServerDecommissioned;
  private const string UserInfoKey = PersistentStateKeys.UserInfo;
  private readonly ILogger<PersistentStateAccessor> _logger;
  private readonly PersistingComponentStateSubscription _persistingSubscription;

  private bool _isDisposed;

  public PersistentStateAccessor(
    PersistentComponentState state,
    ILogger<PersistentStateAccessor> logger)
  {
    _logger = logger;

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
        state.PersistAsJson(DarkModeKey, IsDarkMode);
        state.PersistAsJson(DecommissionStateKey, IsDecommissioned);
        return Task.CompletedTask;
      });
  }

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
