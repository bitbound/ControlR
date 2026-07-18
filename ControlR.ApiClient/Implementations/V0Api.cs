using ControlR.ApiClient.Interfaces.V0;

namespace ControlR.ApiClient;

internal partial class V0Api(ControlrApi client) :
  IControlrV0Api,
  IDesktopSessionsApi,
  IDevicesApi,
  IInstallerKeysApi,
  ILogonTokensApi,
  IServiceAccountsApi,
  ITenantsApi
{
  private readonly ControlrApi _client = client;

  public IDesktopSessionsApi DesktopSessions => this;
  public IDevicesApi Devices => this;
  public IInstallerKeysApi InstallerKeys => this;
  public ILogonTokensApi LogonTokens => this;
  public IServiceAccountsApi ServiceAccounts => this;
  public ITenantsApi Tenants => this;
}