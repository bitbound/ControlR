namespace ControlR.ApiClient.Interfaces.V1;

public interface IControlrV1Api
{
  IDevicesApi Devices { get; }
  IInstallerKeysApi InstallerKeys { get; }
  ILogonTokensApi LogonTokens { get; }
  IServiceAccountsApi ServiceAccounts { get; }
  ITenantsApi Tenants { get; }
}