using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Ratchet for the V1-first rule. Every operation in the internal OpenAPI document must be deprecated,
/// have a V1 twin (same verb and path template under /api/v1), or appear in
/// <see cref="_irregularShapeAllowList"/>. New endpoints belong in V1.
/// </summary>
public partial class InternalV1ParityGuardrailTests
{
  private static readonly string[] _httpVerbs = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

  /// <summary>
  /// Internal operations without a V1 twin, keyed by "VERB /path/template" (route parameters
  /// normalized to "{}"). Each value states why the operation stays internal. An entry becomes stale,
  /// and fails the second test, once its operation is deprecated or gains a V1 twin.
  /// </summary>
  private static readonly Dictionary<string, string> _irregularShapeAllowList = new(StringComparer.Ordinal)
  {
    // MVC identity UI - HTML page flows. No API consumer wants the markup, and the API client
    // cannot read it.
    ["POST /Account/Logout"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",
    ["POST /Account/Manage/DownloadPersonalData"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",
    ["POST /Account/Manage/LinkExternalLogin"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",
    ["POST /Account/PasskeyCreationOptions"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",
    ["POST /Account/PasskeyRequestOptions"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",
    ["POST /Account/PerformExternalLogin"] = "ASP.NET Core Identity MVC HTML page flow, not a resource the API client can read.",

    // Interactive authentication and session lifecycle. These are cookie-session browser
    // ceremonies, and the API client authenticates with headers, so it cannot use them.
    ["GET /api/auth/confirmEmail"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["GET /api/auth/manage/info"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["GET /api/auth/me"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/change-password"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/change-password-with-credentials"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/complete-password-reset"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/forgotPassword"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/interactive-login"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/login"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/logout"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/manage/2fa"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/manage/info"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/refresh"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/register"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/resendConfirmationEmail"] = "Cookie-session browser ceremony; the API client authenticates with headers.",
    ["POST /api/auth/resetPassword"] = "Cookie-session browser ceremony; the API client authenticates with headers.",

    // Agent-facing registration and update negotiation - consumed by the installed agent,
    // versioned by agent-server compatibility rather than API version.
    ["POST /api/agent/devices"] = "Agent-facing device registration.",
    ["POST /api/devices"] = "Legacy agent-registration alias for POST /api/agent/devices.",
    ["GET /api/agent/updates/get-bundle-metadata/{}"] = "Agent update-bundle negotiation.",
    ["GET /api/agent-update/get-bundle-metadata/{}"] = "Legacy agent update-bundle negotiation alias.",

    // Invite acceptance - an anonymous browser ceremony. The emailed token is the credential,
    // and the invitee is a person in a browser, so no API-consumer flow exists to serve.
    ["POST /api/invites/accept"] = "Anonymous browser ceremony; the emailed invite token is the credential, so there is no API-consumer flow to serve.",
  };

  [Fact]
  public void AllowListEntries_AreStillNeeded()
  {
    var internalOps = ReadOperations(Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server", "ControlR.Web.Server_internal.json"));
    var v1Twins = ReadV1TwinKeys();

    var stillUnresolved = internalOps
      .Where(op => !op.Deprecated)
      .Where(op => !v1Twins.Contains(Key(op.Verb, op.Path)))
      .Select(op => Key(op.Verb, op.Path))
      .ToHashSet(StringComparer.Ordinal);

    var stale = _irregularShapeAllowList.Keys
      .Where(key => !stillUnresolved.Contains(key))
      .OrderBy(key => key, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      stale.Length == 0,
      "Allow-list entries whose operation is now deprecated or V1-twinned. Prune them so the " +
      $"guardrail tracks only the remaining non-standard surface: {string.Join(" | ", stale)}");
  }

  [Fact]
  public void InternalOperations_AreDeprecatedTwinnedOrAllowListed()
  {
    var internalOps = ReadOperations(Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server", "ControlR.Web.Server_internal.json"));
    var v1Twins = ReadV1TwinKeys();

    var unclassified = internalOps
      .Where(op => !op.Deprecated)
      .Where(op => !v1Twins.Contains(Key(op.Verb, op.Path)))
      .Where(op => !_irregularShapeAllowList.ContainsKey(Key(op.Verb, op.Path)))
      .Select(op => Key(op.Verb, op.Path))
      .Distinct()
      .OrderBy(key => key, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unclassified.Length == 0,
      "Internal operations with no V1 twin and no allow-list reason. New endpoints belong in V1; " +
      "an Internal endpoint must name the reason the V1 test fails. Handler shape is not a reason. " +
      $"See InternalV1ParityGuardrailTests.IrregularShapeAllowList: {string.Join(" | ", unclassified)}");
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

  private static string Key(string verb, string path)
  {
    return $"{verb} {path}";
  }

  private static List<(string Verb, string Path, bool Deprecated)> ReadOperations(string documentPath)
  {
    using var document = JsonDocument.Parse(File.ReadAllText(documentPath));
    var operations = new List<(string, string, bool)>();

    if (!document.RootElement.TryGetProperty("paths", out var paths))
    {
      return operations;
    }

    foreach (var pathProperty in paths.EnumerateObject())
    {
      var normalizedPath = RouteParameterRegex().Replace(pathProperty.Name, "{}");

      foreach (var verbProperty in pathProperty.Value.EnumerateObject())
      {
        if (!_httpVerbs.Contains(verbProperty.Name, StringComparer.OrdinalIgnoreCase))
        {
          continue;
        }

        var deprecated = verbProperty.Value.TryGetProperty("deprecated", out var deprecatedElement)
          && deprecatedElement.GetBoolean();

        operations.Add((verbProperty.Name.ToUpperInvariant(), normalizedPath, deprecated));
      }
    }

    return operations;
  }

  private static HashSet<string> ReadV1TwinKeys()
  {
    var v1Path = Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server", "ControlR.Web.Server_v1.json");

    return ReadOperations(v1Path)
      .Where(op => op.Path.StartsWith("/api/v1", StringComparison.Ordinal))
      // Re-key the twin into the internal document's coordinate system. Internal routes live at
      // /api/* (no /internal segment), so the twin of /api/v1/users/{} is /api/users/{}.
      .Select(op => Key(op.Verb, "/api" + op.Path["/api/v1".Length..]))
      .ToHashSet(StringComparer.Ordinal);
  }

  [GeneratedRegex(@"\{[^}]*\}")]
  private static partial Regex RouteParameterRegex();
}
