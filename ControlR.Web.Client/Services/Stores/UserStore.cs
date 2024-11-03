namespace ControlR.Web.Client.Services.Stores;

public class UserStore(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  IMessenger messenger,
  ILogger<UserStore> logger)
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserStore> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly SemaphoreSlim _refreshLock = new(1, 1);
  private readonly ISnackbar _snackbar = snackbar;

  public async Task Refresh()
  {
    if (!await _refreshLock.WaitAsync(0))
    {
      // If another thread already acquired the lock, we still want to wait
      // for it to finish, but we don't want to do another refresh.
      await _refreshLock.WaitAsync();
      _refreshLock.Release();
      return;
    }

    try
    {
      // TODO: Get users.
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while refreshing users.");
      _snackbar.Add("Failed to load users", Severity.Error);
    }
    finally
    {
      _refreshLock.Release();
    }
  }
}