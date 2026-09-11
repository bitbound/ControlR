using ControlR.ApiClient.Interfaces.V1;

namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IAuthorizationChangeLogsApi,
  ICustomersApi,
  IDeploymentOptionsApi,
  IDeviceGroupsApi,
  IDevicesApi,
  IEffectivePermissionsApi,
  IInstallerKeysApi,
  ILogonTokensApi,
  IServerServiceAccountsApi,
  ITenantServiceAccountsApi,
  ITenantsApi,
  IUserGroupsApi
{
  private readonly ControlrApi _client = client;

  public IAuthorizationChangeLogsApi AuthorizationChangeLogs => this;
  public ICustomersApi Customers => this;
  public IDeploymentOptionsApi DeploymentOptions => this;
  public IDeviceGroupsApi DeviceGroups => this;
  public IDevicesApi Devices => this;
  public IEffectivePermissionsApi EffectivePermissions => this;
  public IInstallerKeysApi InstallerKeys => this;
  public ILogonTokensApi LogonTokens => this;
  public IServerServiceAccountsApi ServerServiceAccounts => this;
  public ITenantsApi Tenants => this;
  public ITenantServiceAccountsApi TenantServiceAccounts => this;
  public IUserGroupsApi UserGroups => this;
}