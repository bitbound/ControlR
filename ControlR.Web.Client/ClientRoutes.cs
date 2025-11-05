namespace ControlR.Web.Client;

public static class ClientRoutes
{
  public const string About = "/about";
  public const string Deploy = "/deploy";
  public const string Home = "/";
  public const string Invite = "/invite";
  public const string InviteConfirmation = InviteConfirmationBase + "/{activationCode?}";
  public const string InviteConfirmationBase = "/invite-confirmation";
  public const string Permissions = "/permissions";
  public const string PersonalAccessTokens = "/personal-access-tokens";
  public const string ServerSettings = "/server-settings";
  public const string ServerStats = "/server-stats";
  public const string Settings = "/settings";
  public const string TenantSettings = "/tenant-settings";
}
