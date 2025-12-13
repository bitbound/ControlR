using ControlR.Web.Client.Services;
using Microsoft.Extensions.Options;

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
      var registrationEnabled = _appOptions.CurrentValue.EnablePublicRegistration || !await db.Users.AnyAsync();
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
