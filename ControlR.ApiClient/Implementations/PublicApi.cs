namespace ControlR.ApiClient;

internal partial class PublicApi(ControlrApi client) :
  IControlrPublicApi,
  IAgentUpdateApi,
  IPublicInstallerKeysApi,
  IPublicInvitesApi
{
  private readonly ControlrApi _client = client;

  public IAgentUpdateApi AgentUpdate => this;
  public IPublicInstallerKeysApi InstallerKeys => this;
  public IPublicInvitesApi Invites => this;
}