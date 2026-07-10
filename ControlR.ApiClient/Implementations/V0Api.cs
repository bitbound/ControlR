using ControlR.ApiClient.Interfaces.V0;

namespace ControlR.ApiClient;

internal partial class V0Api(ControlrApi client) :
  IControlrV0Api,
  IV0DevicesApi,
  IV0InstallerKeysApi,
  IV0LogonTokensApi,
  IV0TenantsApi
{
  private readonly ControlrApi _client = client;

  public IV0DevicesApi Devices => this;
  public IV0InstallerKeysApi InstallerKeys => this;
  public IV0LogonTokensApi LogonTokens => this;
  public IV0TenantsApi Tenants => this;
}