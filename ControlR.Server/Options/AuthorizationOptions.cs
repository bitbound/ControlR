namespace ControlR.Server.Options;

public class AuthorizationOptions
{
    public const string SectionKey = "Authorization";
    public IReadOnlyList<string> AdminPublicKeys { get; init; } = [];
    public IReadOnlyList<string> AuthorizedUserPublicKeys { get; init; } = [];
    public bool EnableRestrictedUserAccess { get; init; }
}