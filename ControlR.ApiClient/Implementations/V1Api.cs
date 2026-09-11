using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IAuthorizationChangeLogsApi,
  IDeploymentOptionsApi,
  IDevicesApi,
  IEffectivePermissionsApi,
  IInstallerKeysApi,
  ILogonTokensApi,
  IServerServiceAccountsApi,
  ITenantServiceAccountsApi,
  ITenantsApi
{
  private readonly ControlrApi _client = client;

  public IAuthorizationChangeLogsApi AuthorizationChangeLogs => this;
  public IDeploymentOptionsApi DeploymentOptions => this;
  public IDevicesApi Devices => this;
  public IEffectivePermissionsApi EffectivePermissions => this;
  public IInstallerKeysApi InstallerKeys => this;
  public ILogonTokensApi LogonTokens => this;
  public IServerServiceAccountsApi ServerServiceAccounts => this;
  public ITenantsApi Tenants => this;
  public ITenantServiceAccountsApi TenantServiceAccounts => this;
}