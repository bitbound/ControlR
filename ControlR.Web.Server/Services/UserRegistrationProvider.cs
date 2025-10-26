namespace ControlR.Web.Server.Services;

public interface IUserRegistrationProvider
{
  Task<bool> IsSelfRegistrationEnabled();
}

public class UserRegistrationProvider(
  IServiceScopeFactory scopeFactory,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<UserRegistrationProvider> logger) : IUserRegistrationProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly ILogger<UserRegistrationProvider> _logger = logger;
  private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
  public async Task<bool> IsSelfRegistrationEnabled()
  {
    try
    {

      await using var scope = _scopeFactory.CreateAsyncScope();
      await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

      return
        _appOptions.CurrentValue.EnablePublicRegistration ||
        await appDb.Users.AnyAsync() == false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking if self registration is enabled.");
      return false;
    }
  }
}
