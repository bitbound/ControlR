using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Ratchet for the V1-first rule. Every operation published in the internal OpenAPI document
/// must be deprecated, have a V1 twin (same verb and path template under /api/v1), or appear in
/// <see cref="_irregularShapeAllowList"/> naming the constraint that forces it to stay internal.
/// New endpoints belong in V1. Adding an Internal operation requires either a V1 twin or an
/// explicit allow-list entry, so the non-standard surface cannot grow silently.
/// </summary>
public partial class InternalV1ParityGuardrailTests
{
  private static readonly string[] _httpVerbs = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

  /// <summary>
  /// Internal operations without a V1 twin, keyed by "VERB /path/template" (route parameters
  /// normalized to "{}"). Every value states why the operation is not standard CRUD (keeper) or
  /// which migration package will twin it (pending). Entries become stale - and fail the second
  /// test - once their operation is deprecated or gains a V1 twin, which keeps this list honest.
  /// </summary>
  private static readonly Dictionary<string, string> _irregularShapeAllowList = new(StringComparer.Ordinal)
  {
    // MVC identity UI - page-flow endpoints, not REST resources.
    ["POST /Account/Logout"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",
    ["POST /Account/Manage/DownloadPersonalData"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",
    ["POST /Account/Manage/LinkExternalLogin"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",
    ["POST /Account/PasskeyCreationOptions"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",
    ["POST /Account/PasskeyRequestOptions"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",
    ["POST /Account/PerformExternalLogin"] = "ASP.NET Core Identity MVC endpoint, not a REST resource.",

    // Interactive authentication and session lifecycle - no tenant-resource CRUD shape.
    ["GET /api/auth/confirmEmail"] = "Interactive authentication/session lifecycle flow.",
    ["GET /api/auth/manage/info"] = "Interactive authentication/session lifecycle flow.",
    ["GET /api/auth/me"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/change-password"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/change-password-with-credentials"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/complete-password-reset"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/forgotPassword"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/interactive-login"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/login"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/logout"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/manage/2fa"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/manage/info"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/refresh"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/register"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/resendConfirmationEmail"] = "Interactive authentication/session lifecycle flow.",
    ["POST /api/auth/resetPassword"] = "Interactive authentication/session lifecycle flow.",

    // Agent-facing registration and update negotiation - consumed by the installed agent,
    // versioned by agent-server compatibility rather than API version.
    ["POST /api/agent/devices"] = "Agent-facing device registration.",
    ["POST /api/devices"] = "Legacy agent-registration alias for POST /api/agent/devices.",
    ["GET /api/agent/updates/get-bundle-metadata/{}"] = "Agent update-bundle negotiation.",
    ["GET /api/agent-update/get-bundle-metadata/{}"] = "Legacy agent update-bundle negotiation alias.",

    // Live-session/interactive surfaces - operate on a running device, not stored resources.
    ["GET /api/desktop-preview/{}/{}"] = "Remote-desktop preview handshake.",
    ["DELETE /api/device-file-system/delete-path/{}"] = "Interactive file operation against a live agent session.",
    ["GET /api/device-file-system/download/{}"] = "Interactive file operation against a live agent session.",
    ["GET /api/device-file-system/logs/{}"] = "Interactive file operation against a live agent session.",
    ["GET /api/device-file-system/logs/{}/contents"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/contents"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/create-directory/{}"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/download-archive/{}"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/path-segments"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/root-drives"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/subdirectories"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/upload/{}"] = "Interactive file operation against a live agent session.",
    ["POST /api/device-file-system/validate-path/{}"] = "Interactive file operation against a live agent session.",

    // Public/diagnostic probes - pre-auth or server-ops, not tenant CRUD.
    ["GET /api/version/agent"] = "Public version/release-notes discovery probe.",
    ["GET /api/version/release-notes"] = "Public version/release-notes discovery probe.",
    ["GET /api/version/server"] = "Public version/release-notes discovery probe.",
    ["GET /api/public-server-settings"] = "Pre-authentication server discovery.",
    ["GET /api/server-alert"] = "Server alert polling.",
    ["POST /api/server-alert"] = "Server alert acknowledgement.",
    ["GET /api/server-logs/get-aspire-url"] = "Server diagnostics probe.",
    ["GET /api/server-stats"] = "Server diagnostics snapshot.",
    ["POST /api/test-email"] = "SMTP connectivity test action.",
    ["GET /api/user-server-settings/decommission-status"] = "Client-environment capability probe.",
    ["GET /api/user-server-settings/file-upload-max-size"] = "Client-environment capability probe.",
    ["POST /api/invites/accept"] = "Anonymous token-bearing accept ceremony; the activation code in the invite URL is the credential, not a principal.",

    // Pending V1 twins - migration packages prune these entries when the twin lands.
    // The full migration surface is complete as of this revision. Any new pending entry
    // added here must name the package that will remove it.
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
      "an Internal endpoint must name the irregular-shape constraint that forces it in " +
      $"InternalV1ParityGuardrailTests.IrregularShapeAllowList: {string.Join(" | ", unclassified)}");
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
