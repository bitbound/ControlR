namespace ControlR.ApiClient;

internal partial class V1Api(ControlrApi client) :
  IControlrV1Api,
  IV1InstallerKeysApi,
  IV1LogonTokensApi,
  IV1TenantsApi
{
  private readonly ControlrApi _client = client;

  public IV1InstallerKeysApi InstallerKeys => this;
  public IV1LogonTokensApi LogonTokens => this;
  public IV1TenantsApi Tenants => this;
}