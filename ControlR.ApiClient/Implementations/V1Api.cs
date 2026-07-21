using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IDevicesApi,
  IInstallerKeysApi,
  ILogonTokensApi,
  IServiceAccountsApi,
  ITenantsApi
{
  private readonly ControlrApi _client = client;

  public IDevicesApi Devices => this;
  public IInstallerKeysApi InstallerKeys => this;
  public ILogonTokensApi LogonTokens => this;
  public IServiceAccountsApi ServiceAccounts => this;
  public ITenantsApi Tenants => this;
}