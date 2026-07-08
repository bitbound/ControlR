namespace ControlR.ApiClient;

internal partial class V0Api(ControlrApi client) :
  IControlrV0Api,
  IV0InstallerKeysApi,
  IV0LogonTokensApi,
  IV0TenantsApi
{
  private readonly ControlrApi _client = client;

  public IV0InstallerKeysApi InstallerKeys => this;
  public IV0LogonTokensApi LogonTokens => this;
  public IV0TenantsApi Tenants => this;
}