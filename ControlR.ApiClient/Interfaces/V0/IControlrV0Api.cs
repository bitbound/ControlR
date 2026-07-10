namespace ControlR.ApiClient.Interfaces.V0;

public interface IControlrV0Api
{
  IV0DevicesApi Devices { get; }
  IV0InstallerKeysApi InstallerKeys { get; }
  IV0LogonTokensApi LogonTokens { get; }
  IV0TenantsApi Tenants { get; }
}