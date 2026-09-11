namespace ControlR.ApiClient.Interfaces.V1;

public interface IControlrV1Api
{
  IAuthorizationChangeLogsApi AuthorizationChangeLogs { get; }
  IDeploymentOptionsApi DeploymentOptions { get; }
  IDevicesApi Devices { get; }
  IEffectivePermissionsApi EffectivePermissions { get; }
  IInstallerKeysApi InstallerKeys { get; }
  ILogonTokensApi LogonTokens { get; }
  IServerServiceAccountsApi ServerServiceAccounts { get; }
  ITenantsApi Tenants { get; }
  ITenantServiceAccountsApi TenantServiceAccounts { get; }
}