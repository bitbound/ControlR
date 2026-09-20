using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Fails when the committed V1 OpenAPI document breaks shape against the last customer-shipped release.
/// Additive changes pass. Removed paths/operations/status codes, removed or renamed response fields, and
/// changed scalar field types fail.
/// Known scope limits: does not check enum-member removal, request-side required additions, or parameter
/// requiredness. Those are intentional, narrower than a full breaking-change gate.
/// </summary>
public class V1ContractShapeGuardTests
{
  // The last tag actually shipped to customers. The newest tag (v0.28.x) is private/non-shipped, so
  // git describe is the wrong baseline here.
  private const string BaselineRef = "v0.27.6.0";

  private const string DocumentRepoPath = "ControlR.Web.Server/ControlR.Web.Server_v1.json";

  // Removals that already shipped to main during V1 finalization, before this guard existed. They are
  // grandfathered so the gate is green at HEAD and only bites on drift introduced after it lands.
  // service-accounts was restructured into the server- and tenant-scoped surfaces; CreatorKind and
  // DesktopSessionType were replaced by their V1-scoped equivalents.
  private static readonly HashSet<string> _grandfatheredRemovedPaths =
    new(StringComparer.Ordinal)
    {
      "/api/v1/service-accounts",
      "/api/v1/service-accounts/{serviceAccountId}",
      "/api/v1/service-accounts/{serviceAccountId}/credentials",
      "/api/v1/service-accounts/{serviceAccountId}/credentials/{credentialId}",
      "/api/v1/tenants/{id}",
    };

  private static readonly HashSet<string> _grandfatheredRemovedSchemas =
    new(StringComparer.Ordinal)
    {
      "CreateServiceAccountResponseDto",
      "CreatorKind",
      "DesktopSessionType",
      "ServiceAccountDto",
    };

  private static readonly string[] _httpVerbs = ["get", "put", "post", "delete", "patch", "head", "options", "trace"];

  [Fact]
  public void V1Contract_HasNoBreakingShapeChangesAgainstReleasedBaseline()
  {
    var currentPath = Path.Combine(FindRepositoryRoot(), "ControlR.Web.Server", "ControlR.Web.Server_v1.json");
    Assert.True(File.Exists(currentPath), $"Missing committed V1 document: {currentPath}");

    using var current = JsonDocument.Parse(File.ReadAllText(currentPath));
    using var baseline = JsonDocument.Parse(ReadBaselineDocument());

    var responseSchemas = BuildResponseSchemaNames(baseline.RootElement);
    var breaking = new List<string>();
    CompareEndpoints(baseline.RootElement, current.RootElement, breaking);
    CompareSchemas(baseline.RootElement, current.RootElement, responseSchemas, breaking);
    FindStaleGrandfatherEntries(current.RootElement, breaking);

    Assert.True(
      breaking.Count == 0,
      $"The V1 contract is a breaking change against {BaselineRef}. V1 is the stable customer contract; " +
      "removing a path, operation, success status code, or a response field, or changing a field type, " +
      "breaks clients built from the published document. Add new members instead, or bump the version. " +
      "If a removal is an intentional part of finalizing V1, grandfather it in this test with a reason. " +
      $"Breaks:\n  {string.Join("\n  ", breaking)}");
  }

  private static void AddSchemaRef(JsonElement schema, HashSet<string> names)
  {
    if (schema.ValueKind != JsonValueKind.Object)
    {
      return;
    }

    if (schema.TryGetProperty("$ref", out var directRef) && directRef.ValueKind == JsonValueKind.String)
    {
      names.Add(RefName(directRef.GetString() ?? string.Empty));
      return;
    }

    if (schema.TryGetProperty("items", out var items)
      && items.ValueKind == JsonValueKind.Object
      && items.TryGetProperty("$ref", out var itemRef)
      && itemRef.ValueKind == JsonValueKind.String)
    {
      names.Add(RefName(itemRef.GetString() ?? string.Empty));
    }
  }

  private static HashSet<string> BuildResponseSchemaNames(JsonElement root)
  {
    var names = new HashSet<string>(StringComparer.Ordinal);
    var paths = TryGetObject(root, ["paths"]);
    if (paths is null)
    {
      return names;
    }

    foreach (var path in paths.Value.EnumerateObject())
    {
      foreach (var operation in path.Value.EnumerateObject())
      {
        if (!_httpVerbs.Contains(operation.Name, StringComparer.OrdinalIgnoreCase))
        {
          continue;
        }

        var responses = TryGetObject(operation.Value, ["responses"]);
        if (responses is null)
        {
          continue;
        }

        foreach (var response in responses.Value.EnumerateObject())
        {
          if (response.Name.Length != 3 || response.Name[0] != '2')
          {
            continue;
          }

          var content = TryGetObject(response.Value, ["content"]);
          if (content is null)
          {
            continue;
          }

          foreach (var mediaType in content.Value.EnumerateObject())
          {
            if (TryGetObject(mediaType.Value, ["schema"]) is { } schema)
            {
              AddSchemaRef(schema, names);
            }
          }
        }
      }
    }

    // Expand transitively. A DTO reachable only through a nested property $ref (for example the Drive
    // list inside a device response) is still a shape served to clients, so a field removed from it is
    // a break too. The visited set makes the walk terminate on cyclic schemas.
    var components = TryGetObject(root, ["components", "schemas"]);
    var worklist = new Queue<string>(names);
    while (worklist.Count > 0)
    {
      var name = worklist.Dequeue();
      if (components is null
        || !components.Value.TryGetProperty(name, out var definition)
        || definition.ValueKind != JsonValueKind.Object)
      {
        continue;
      }

      foreach (var nested in CollectNestedSchemaNames(definition))
      {
        if (names.Add(nested))
        {
          worklist.Enqueue(nested);
        }
      }
    }

    return names;
  }

  private static IEnumerable<string> CollectNestedSchemaNames(JsonElement schemaDefinition)
  {
    var properties = TryGetObject(schemaDefinition, ["properties"]);
    if (properties is null)
    {
      return [];
    }

    var refs = new HashSet<string>(StringComparer.Ordinal);
    foreach (var property in properties.Value.EnumerateObject())
    {
      AddSchemaRef(property.Value, refs);
    }

    return refs;
  }

  private static void CompareEndpoints(JsonElement baselineRoot, JsonElement currentRoot, List<string> breaking)
  {
    var baselinePaths = TryGetObject(baselineRoot, ["paths"]);
    var currentPaths = TryGetObject(currentRoot, ["paths"]);
    if (baselinePaths is null || currentPaths is null)
    {
      return;
    }

    foreach (var baselinePath in baselinePaths.Value.EnumerateObject())
    {
      if (_grandfatheredRemovedPaths.Contains(baselinePath.Name))
      {
        continue;
      }

      if (!currentPaths.Value.TryGetProperty(baselinePath.Name, out var currentPath))
      {
        breaking.Add($"path removed or renamed: {baselinePath.Name}");
        continue;
      }

      foreach (var baselineOperation in baselinePath.Value.EnumerateObject())
      {
        if (!_httpVerbs.Contains(baselineOperation.Name, StringComparer.OrdinalIgnoreCase))
        {
          continue;
        }

        if (!currentPath.TryGetProperty(baselineOperation.Name, out var currentOperation))
        {
          breaking.Add($"operation removed: {baselineOperation.Name.ToUpperInvariant()} {baselinePath.Name}");
          continue;
        }

        var baselineResponses = TryGetObject(baselineOperation.Value, ["responses"]);
        var currentResponses = TryGetObject(currentOperation, ["responses"]);
        if (baselineResponses is null || currentResponses is null)
        {
          continue;
        }

        foreach (var statusCode in baselineResponses.Value.EnumerateObject())
        {
          if (statusCode.Name.Length == 3
            && statusCode.Name[0] == '2'
            && !currentResponses.Value.TryGetProperty(statusCode.Name, out _))
          {
            breaking.Add($"success status {statusCode.Name} removed: {baselineOperation.Name.ToUpperInvariant()} {baselinePath.Name}");
          }
        }
      }
    }
  }

  private static void CompareSchemas(
    JsonElement baselineRoot,
    JsonElement currentRoot,
    HashSet<string> responseSchemas,
    List<string> breaking)
  {
    var baselineSchemas = TryGetObject(baselineRoot, ["components", "schemas"]);
    var currentSchemas = TryGetObject(currentRoot, ["components", "schemas"]);
    if (baselineSchemas is null || currentSchemas is null)
    {
      return;
    }

    foreach (var baselineSchema in baselineSchemas.Value.EnumerateObject())
    {
      if (!currentSchemas.Value.TryGetProperty(baselineSchema.Name, out var currentSchema))
      {
        if (!_grandfatheredRemovedSchemas.Contains(baselineSchema.Name))
        {
          breaking.Add($"schema removed or renamed: {baselineSchema.Name}");
        }

        continue;
      }

      var baselineProperties = TryGetObject(baselineSchema.Value, ["properties"]);
      var currentProperties = TryGetObject(currentSchema, ["properties"]);
      if (baselineProperties is null)
      {
        continue;
      }

      // A field that only ever appeared on a request is safe to drop (old clients still send it and it
      // is ignored). Only removing a property from a shape served back to clients breaks them.
      var isResponseShape = responseSchemas.Contains(baselineSchema.Name);

      foreach (var property in baselineProperties.Value.EnumerateObject())
      {
        if (currentProperties is null || !currentProperties.Value.TryGetProperty(property.Name, out var currentProperty))
        {
          if (isResponseShape)
          {
            breaking.Add($"response property removed or renamed: {baselineSchema.Name}.{property.Name}");
          }

          continue;
        }

        // Flag a type change only when both sides declare a concrete primitive, to avoid noise from
        // freeform / object / $ref nodes whose declared type legitimately varies with generated output.
        var baselineType = ScalarType(property.Value);
        var currentType = ScalarType(currentProperty);
        if (baselineType is not null
          && currentType is not null
          && !string.Equals(baselineType, currentType, StringComparison.Ordinal))
        {
          breaking.Add($"property type changed: {baselineSchema.Name}.{property.Name} {baselineType} -> {currentType}");
        }
      }
    }
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

  private static void FindStaleGrandfatherEntries(JsonElement currentRoot, List<string> breaking)
  {
    var currentPaths = TryGetObject(currentRoot, ["paths"]);
    var currentSchemas = TryGetObject(currentRoot, ["components", "schemas"]);

    foreach (var path in _grandfatheredRemovedPaths)
    {
      if (currentPaths is not null && currentPaths.Value.TryGetProperty(path, out _))
      {
        breaking.Add($"grandfathered path is present again, remove it from the allowlist: {path}");
      }
    }

    foreach (var schema in _grandfatheredRemovedSchemas)
    {
      if (currentSchemas is not null && currentSchemas.Value.TryGetProperty(schema, out _))
      {
        breaking.Add($"grandfathered schema is present again, remove it from the allowlist: {schema}");
      }
    }
  }

  private static string? NonNullScalar(JsonElement type)
  {
    if (type.ValueKind != JsonValueKind.Array)
    {
      return null;
    }

    string? scalar = null;
    foreach (var entry in type.EnumerateArray())
    {
      if (entry.ValueKind != JsonValueKind.String || entry.GetString() == "null")
      {
        return null;
      }

      if (scalar is not null)
      {
        return null;
      }

      scalar = entry.GetString();
    }

    return scalar;
  }

  private static string ReadBaselineDocument()
  {
    var repositoryRoot = FindRepositoryRoot();

    // Full history and tags are required; CI checks out with fetch-depth: 0. A shallow clone without
    // the baseline tag fails here rather than skipping, so the guard can never pass by being unmeasurable.
    using var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = "git",
        Arguments = $"show {BaselineRef}:{DocumentRepoPath}",
        WorkingDirectory = repositoryRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        UseShellExecute = false
      }
    };

    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
    {
      process.Kill(entireProcessTree: true);
      throw new InvalidOperationException($"git show for {BaselineRef} timed out after 15s.");
    }

    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
    {
      throw new InvalidOperationException(
        $"Cannot read {DocumentRepoPath} at {BaselineRef} (exit {process.ExitCode}). Fetch tags and full " +
        $"history: git fetch --tags --unshallow. git said: {error}");
    }

    return output;
  }

  private static string RefName(string reference)
  {
    var slash = reference.LastIndexOf('/');
    return slash >= 0 ? reference[(slash + 1)..] : reference;
  }

  private static string? ScalarType(JsonElement schema)
  {
    if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type))
    {
      return null;
    }

    // OpenAPI 3.1 marks a nullable scalar as ["null", "<type>"]. Reduce that to the single concrete type
    // so a nullable field's type change is still detected. A wider union is not treated as a primitive.
    var candidate = type.ValueKind == JsonValueKind.String ? type.GetString() : NonNullScalar(type);
    return candidate is not null && candidate is not ("object" or "array") ? candidate : null;
  }

  private static JsonElement? TryGetObject(JsonElement root, string[] breadcrumb)
  {
    var node = root;
    foreach (var key in breadcrumb)
    {
      if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node))
      {
        return null;
      }
    }

    return node.ValueKind == JsonValueKind.Object ? node : null;
  }
}
