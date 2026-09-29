using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Web.Client.Services;

namespace ControlR.Web.Server.Services.Users;

internal class PublicServerSettingsProviderServer(
  IDbContextFactory<AppDb> dbFactory,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<PublicServerSettingsProviderServer> logger) : IPublicServerSettingsProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IDbContextFactory<AppDb> _dbFactory = dbFactory;
  private readonly ILogger<PublicServerSettingsProviderServer> _logger = logger;

  public async Task<PublicServerSettings> GetPublicServerSettings()
  {
    try
    {
      var appOpts = _appOptions.CurrentValue;
      await using var db = await _dbFactory.CreateDbContextAsync();
      var hasUsers = await db.Users.AnyAsync();
      var registrationEnabled = appOpts.EnablePublicRegistration ||
        (!appOpts.DisableFirstUserSelfRegistration && !hasUsers);

      return new PublicServerSettings(
        IsPublicRegistrationEnabled: registrationEnabled,
        DisableDesktopPreview: appOpts.DisableDesktopPreview);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting public server settings.");
      return new PublicServerSettings(
        IsPublicRegistrationEnabled: false,
        DisableDesktopPreview: _appOptions.CurrentValue.DisableDesktopPreview);
    }
  }
}
