namespace ControlR.ApiClient.Interfaces.V0;

public interface IControlrV0Api
{
  IDevicesApi Devices { get; }
  IInstallerKeysApi InstallerKeys { get; }
  ILogonTokensApi LogonTokens { get; }
  IServiceAccountsApi ServiceAccounts { get; }
  ITenantsApi Tenants { get; }
}