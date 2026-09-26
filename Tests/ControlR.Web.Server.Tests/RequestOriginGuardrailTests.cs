namespace ControlR.Web.Server.Tests;

/// <summary>
/// Enforces the outbound-URL convention. Every absolute URL this server hands to someone is built
/// through <see cref="Services.IPublicUrlProvider"/> from the configured <c>AppOptions:PublicBaseUrl</c>.
/// Anything that derives an origin from the request is banned in server code, because those values are
/// caller-controlled and a URL built from them can be aimed at an attacker's host.
/// </summary>
/// <remarks>
/// <para>
/// This is a source scan rather than an entry in <c>BannedSymbols.txt</c> on purpose. RS0030 is raised
/// only where <c>.editorconfig</c> makes it an error, which is <c>Api/V1</c> alone, so a symbol ban would
/// be inert across the pages that actually built these URLs. RS0030 also carries one severity for the
/// whole list, so raising it project-wide would ban the Internal routes' bare-string error shortcuts
/// along with it.
/// </para>
/// <para>
/// Ref: https://github.com/bitbound/ControlR/issues/175
/// </para>
/// </remarks>
public class RequestOriginGuardrailTests
{

  /// <summary>
  /// Files allowed to derive an absolute URI from the request. <c>IdentityRedirectManager</c> resolves the
  /// current request to build a redirect back to the same caller, which never leaves the server.
  /// </summary>
  private static readonly string[] _allowList =
  [
    @"Components\Account\IdentityRedirectManager.cs",
  ];
  private static readonly string[] _bannedPatterns =
  [
    "Request.ToOrigin(",
    "Request.Scheme",
    "Request.Host",
    "request.Scheme",
    "request.Host",
    // NavigationManager resolves absolute URIs against the current request in SSR, which is how the
    // password-reset link took its origin from a forged header. GetUriWithQueryParameters on a relative
    // path is fine, because it stays relative for the same caller.
    "ToAbsoluteUri(",
  ];

  [Fact]
  public void ServerCode_DoesNotBuildUrlsFromTheRequestOrigin()
  {
    var serverRoot = Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server");
    var offenders = new List<string>();

    foreach (var file in EnumerateServerSourceFiles(serverRoot))
    {
      var relative = Path.GetRelativePath(serverRoot, file);

      if (IsExcluded(relative) || _allowList.Any(allowed => relative.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)))
      {
        continue;
      }

      var text = File.ReadAllText(file);
      if (_bannedPatterns.Any(pattern => text.Contains(pattern, StringComparison.Ordinal)))
      {
        offenders.Add(relative);
      }
    }

    Assert.True(
      offenders.Count == 0,
      "These files derive a URL origin from the request instead of IPublicUrlProvider. Build outbound " +
      "URLs with IPublicUrlProvider.TryGetAbsoluteUrl, which uses the configured AppOptions:PublicBaseUrl. " +
      $"If a file legitimately needs the request origin, add it to {nameof(RequestOriginGuardrailTests)}.{nameof(_allowList)} " +
      $"with a reason: {string.Join(" | ", offenders)}");
  }

  /// <summary>
  /// Server sources that can build a URL. Razor pages are included because the pages, not the
  /// controllers, were where the request-derived links lived.
  /// </summary>
  private static IEnumerable<string> EnumerateServerSourceFiles(string serverRoot)
  {
    return Directory
      .EnumerateFiles(serverRoot, "*.cs", SearchOption.AllDirectories)
      .Concat(Directory.EnumerateFiles(serverRoot, "*.razor", SearchOption.AllDirectories));
  }

  private static string FindRepositoryRoot()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
      if (File.Exists(Path.Combine(current.FullName, "ControlR.slnx")))
      {
        return current.FullName;
      }

      current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing ControlR.slnx.");
  }

  private static bool IsExcluded(string relativePath)
  {
    return relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
      relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
      relativePath.StartsWith("Data" + Path.DirectorySeparatorChar + "Migrations", StringComparison.OrdinalIgnoreCase);
  }
}
