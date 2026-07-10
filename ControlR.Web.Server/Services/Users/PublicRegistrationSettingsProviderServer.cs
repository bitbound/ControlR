using ControlR.Web.Client.Services;

namespace ControlR.Web.Server.Services.Users;

internal class PublicRegistrationSettingsProviderServer(
  IDbContextFactory<AppDb> dbFactory,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<PublicRegistrationSettingsProviderServer> logger) : IPublicRegistrationSettingsProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IDbContextFactory<AppDb> _dbFactory = dbFactory;
  private readonly ILogger<PublicRegistrationSettingsProviderServer> _logger = logger;

  private bool? _cachedValue;

  public async Task<bool> GetIsPublicRegistrationEnabled()
  {
    if (_cachedValue.HasValue)
    {
      return _cachedValue.Value;
    }

    try
    {
      await using var db = await _dbFactory.CreateDbContextAsync();
      var hasUsers = await db.Users.AnyAsync();
      var registrationEnabled = _appOptions.CurrentValue.EnablePublicRegistration ||
        (_appOptions.CurrentValue.EnableFirstUserSelfRegistration && !hasUsers);
      _cachedValue = registrationEnabled;
      return registrationEnabled;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting public registration settings.");
      return false;
    }
  }
}
