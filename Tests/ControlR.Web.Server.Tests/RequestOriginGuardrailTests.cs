namespace ControlR.Web.Server.Tests;

/// <summary>
/// Enforces the outbound-URL convention. Every absolute URL this server hands to someone is built
/// through <see cref="Services.IPublicUrlProvider"/> from the configured <c>AppOptions:PublicBaseUrl</c>.
/// Deriving an origin from the request (<c>Request.Scheme</c>/<c>Request.Host</c>) is banned in
/// server code, because those values are caller-controlled and a URL built from them can be aimed
/// at an attacker's host.
/// </summary>
/// <remarks>
/// Ref: https://github.com/bitbound/ControlR/issues/175
/// </remarks>
public class RequestOriginGuardrailTests
{

  /// <summary>
  /// Files allowed to touch the request origin. <c>PublicUrlProvider</c> is the single place that
  /// reads the request, and <c>EmailSender</c> checks the host only to decide whether to inline a
  /// logo, never to build a link.
  /// </summary>
  private static readonly string[] _allowList =
  [
    @"Services\PublicUrlProvider.cs",
    @"Services\EmailSender.cs",
  ];
  private static readonly string[] _bannedPatterns =
  [
    "Request.ToOrigin(",
    "Request.Scheme",
    "Request.Host",
    "request.Scheme",
    "request.Host",
  ];

  [Fact]
  public void ServerCode_DoesNotBuildUrlsFromTheRequestOrigin()
  {
    var serverRoot = Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server");
    var offenders = new List<string>();

    foreach (var file in Directory.EnumerateFiles(serverRoot, "*.cs", SearchOption.AllDirectories))
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
