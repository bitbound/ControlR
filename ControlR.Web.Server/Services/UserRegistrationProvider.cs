namespace ControlR.Web.Server.Services;

public interface IUserRegistrationProvider
{
  /// <summary>
  /// Acquires a lock around public registration.  This is necessary to prevent multiple first-time
  /// registration requests from slipping through simultaneously, which could result in multiple admin accounts being created.
  /// </summary>
  Task<IDisposable> AcquirePublicRegistrationLock(CancellationToken cancellationToken);
  Task<bool> IsPublicRegistrationEnabled();
}

public class UserRegistrationProvider(
  IServiceScopeFactory scopeFactory,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<UserRegistrationProvider> logger) : IUserRegistrationProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly ILogger<UserRegistrationProvider> _logger = logger;
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public async Task<IDisposable> AcquirePublicRegistrationLock(CancellationToken cancellationToken)
  {
    return await _semaphore.AcquireLockAsync(cancellationToken);
  }

  public async Task<bool> IsPublicRegistrationEnabled()
  {
    try
    {
      await using var scope = _scopeFactory.CreateAsyncScope();
      await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

      var hasUsers = await appDb.Users.AnyAsync();

      return _appOptions.CurrentValue.EnablePublicRegistration ||
        (_appOptions.CurrentValue.EnableFirstUserBootstrap && !hasUsers);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking if self registration is enabled.");
      return false;
    }
  }
}
