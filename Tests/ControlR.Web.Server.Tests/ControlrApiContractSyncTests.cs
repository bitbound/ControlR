using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlR.Web.Server.Tests;

public partial class ControlrApiContractSyncTests
{
  [Fact]
  public void HttpConstants_AreExplicitlyMappedToIControlrApiSubClients()
  {
    var endpointPropertyMap = CreateEndpointPropertyMap();
    var constantValues = GetHttpConstantValues();

    var actualPropertyNames = GetAllSubInterfacePropertyNames()
      .OrderBy(name => name)
      .ToArray();

    foreach (var constantValue in constantValues)
    {
      Assert.True(
        endpointPropertyMap.ContainsKey(constantValue),
        $"Missing endpoint-to-sub-client mapping for HttpConstants route '{constantValue}'.");
    }

    foreach (var mapping in endpointPropertyMap)
    {
      Assert.Contains(mapping.Key, constantValues);
      Assert.Contains(mapping.Value, actualPropertyNames);
    }
  }

  [Fact]
  public void OpenApiApiPaths_AreCoveredByClientRouteTemplates()
  {
    var apiData = LoadOpenApiTestData();
    var constantValues = GetHttpConstantValues();
    var clientTemplates = LoadClientRouteTemplates(constantValues);

    var missingPaths = apiData.NonLegacyPaths
      .Where(path => !clientTemplates.Contains(path))
      .OrderBy(path => path)
      .ToArray();

    var groupedMissingPaths = missingPaths
      .GroupBy(path =>
      {
        var hasMatch = TryGetLongestMatchingConstant(path, constantValues, out var matchedConstant);
        return hasMatch
          ? NormalizePathTemplate(matchedConstant ?? string.Empty)
          : "<no-http-constant-match>";
      })
      .OrderBy(group => group.Key)
      .Select(group => $"{group.Key}: [{string.Join(", ", group.OrderBy(item => item))}]")
      .ToArray();

    var groupedMissingPathsMessage = groupedMissingPaths.Length == 0
      ? "none"
      : string.Join(" | ", groupedMissingPaths);

    Assert.True(
      missingPaths.Length == 0,
      $"Client route templates are missing OpenAPI paths: {string.Join(", ", missingPaths)}. Grouped by base endpoint: {groupedMissingPathsMessage}");
  }

  [Fact]
  public void OpenApiApiPaths_AreCoveredByHttpConstants()
  {
    var apiData = LoadOpenApiTestData();
    var constantValues = GetHttpConstantValues();

    foreach (var apiPath in apiData.AllPaths)
    {
      var hasMatchingConstant = TryGetLongestMatchingConstant(apiPath, constantValues, out _);

      Assert.True(
        hasMatchingConstant,
        $"No HttpConstants route covers OpenAPI path '{apiPath}'.");
    }
  }

  [Fact]
  public void OpenApiApiPaths_MapToKnownIControlrApiSubClients()
  {
    var apiData = LoadOpenApiTestData();
    var constantValues = GetHttpConstantValues();
    var endpointPropertyMap = CreateEndpointPropertyMap();
    var actualPropertyNames = GetAllSubInterfacePropertyNames();

    foreach (var apiPath in apiData.AllPaths)
    {
      var hasMatchingConstant = TryGetLongestMatchingConstant(apiPath, constantValues, out var matchedConstant);

      Assert.True(
        hasMatchingConstant,
        $"No HttpConstants route covers OpenAPI path '{apiPath}'.");

      Assert.NotNull(matchedConstant);

      var hasMapping = endpointPropertyMap.TryGetValue(matchedConstant ?? string.Empty, out var propertyName);

      Assert.True(
        hasMapping,
        $"No endpoint-to-sub-client mapping exists for OpenAPI path '{apiPath}' using route '{matchedConstant}'.");

      Assert.True(
        actualPropertyNames.Contains(propertyName ?? string.Empty),
        $"Mapped sub-client '{propertyName}' for route '{matchedConstant}' is not present on any sub-interface.");
    }
  }

  private record OpenApiTestData(
    string[] AllPaths,
    string[] NonLegacyPaths);

  private static Dictionary<string, string> CreateEndpointPropertyMap()
  {
    var propertyNames = GetAllSubInterfacePropertyNames();
    var endpointPropertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var field in GetAllHttpConstantFields().OrderBy(field => field.Name))
    {
      var constantValue = field.GetValue(null) as string;

      if (string.IsNullOrWhiteSpace(constantValue))
      {
        continue;
      }

      var propertyName = field.Name.EndsWith("Endpoint", StringComparison.Ordinal)
        ? field.Name[..^"Endpoint".Length]
        : field.Name;

      Assert.True(
        propertyNames.Contains(propertyName),
        $"HttpConstants field '{field.Name}' (declared by '{field.DeclaringType?.Name}') inferred property '{propertyName}', but that property was not found on any sub-interface.");

      endpointPropertyMap[constantValue] = propertyName;
    }

    return endpointPropertyMap;
  }

  private static string FindRepositoryRoot()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
      var candidatePath = Path.Combine(current.FullName, "ControlR.slnx");
      if (File.Exists(candidatePath))
      {
        return current.FullName;
      }

      current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing ControlR.slnx.");
  }

  private static IEnumerable<FieldInfo> GetAllHttpConstantFields()
  {
    static IEnumerable<FieldInfo> GetFieldsRecursive(Type type)
    {
      foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(string) && f.IsLiteral))
      {
        yield return field;
      }

      foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
      {
        foreach (var field in GetFieldsRecursive(nested))
        {
          yield return field;
        }
      }
    }

    return GetFieldsRecursive(typeof(HttpConstants));
  }

  private static HashSet<string> GetAllSubInterfacePropertyNames()
  {
    var propertyNames = new HashSet<string>(StringComparer.Ordinal);

    foreach (var type in new[]
    {
      typeof(IControlrInternalApi),
      typeof(IControlrV0Api),
    })
    {
      foreach (var propertyName in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(p => p.Name))
      {
        propertyNames.Add(propertyName);
      }
    }

    return propertyNames;
  }

  private static string[] GetHttpConstantValues()
  {
    var values = GetAllHttpConstantFields()
      .Select(field => field.GetValue(null) as string)
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Select(value => value ?? string.Empty)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return values;
  }

  [GeneratedRegex("HttpConstants\\.(?<name>[A-Za-z0-9_.]+)")]
  private static partial Regex HttpConstantRegex();

  [GeneratedRegex("\\{HttpConstants\\.(?<name>[A-Za-z0-9_.]+)\\}")]
  private static partial Regex InterpolatedConstantRegex();

  [GeneratedRegex("\\$\\\"(?<value>[^\\\"]*HttpConstants\\.[A-Za-z0-9_.]+[^\\\"]*)\\\"")]
  private static partial Regex InterpolatedStringRegex();

  [GeneratedRegex("\\{[^{}]+\\}")]
  private static partial Regex InterpolationBlockRegex();

  private static HashSet<string> LoadClientRouteTemplates(IReadOnlyCollection<string> constantValues)
  {
    var repositoryRoot = FindRepositoryRoot();
    var apiClientDirectory = Path.Combine(repositoryRoot, "ControlR.ApiClient", "Implementations");
    var sourceFiles = Directory
      .EnumerateFiles(apiClientDirectory, "*.cs", SearchOption.TopDirectoryOnly)
      .Where(path =>
      {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("InternalApi", StringComparison.OrdinalIgnoreCase) ||
          fileName.StartsWith("V0Api", StringComparison.OrdinalIgnoreCase);
      })
      .ToArray();

    var templates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var sourceFile in sourceFiles)
    {
      var source = File.ReadAllText(sourceFile);

      foreach (Match match in InterpolatedStringRegex().Matches(source))
      {
        var rawTemplate = match.Groups["value"].Value;
        var resolvedTemplate = ResolveTemplate(rawTemplate);
        var normalizedTemplate = NormalizePathTemplate(resolvedTemplate);

        if (normalizedTemplate.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
          templates.Add(normalizedTemplate);
        }
      }

      foreach (Match match in HttpConstantRegex().Matches(source))
      {
        var constantName = match.Groups["name"].Value;
        if (!TryResolveHttpConstantValue(constantName, out var constantValue))
        {
          continue;
        }

        var normalizedTemplate = NormalizePathTemplate(constantValue);
        if (normalizedTemplate.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
          templates.Add(normalizedTemplate);
        }
      }
    }

    foreach (var value in constantValues)
    {
      templates.Add(NormalizePathTemplate(value));
    }

    return templates;
  }

  private static OpenApiTestData LoadOpenApiTestData()
  {
    var repositoryRoot = FindRepositoryRoot();
    var serverDirectory = Path.Combine(repositoryRoot, "ControlR.Web.Server");

    var jsonFiles = Directory.GetFiles(serverDirectory, "ControlR.Web.Server_*.json");
    Assert.True(jsonFiles.Length > 0, $"No OpenAPI JSON files found in '{serverDirectory}'.");

    var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var nonLegacyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var jsonFile in jsonFiles)
    {
      var isLegacy = Path.GetFileName(jsonFile).Contains("legacy", StringComparison.OrdinalIgnoreCase);

      using var stream = File.OpenRead(jsonFile);
      using var document = JsonDocument.Parse(stream);

      var root = document.RootElement;
      if (!root.TryGetProperty("paths", out var pathsElement))
      {
        continue;
      }

      var paths = pathsElement
        .EnumerateObject()
        .Select(property => property.Name)
        .Where(path => path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        .Select(NormalizePathTemplate)
        .Distinct(StringComparer.OrdinalIgnoreCase);

      foreach (var path in paths)
      {
        allPaths.Add(path);
        if (!isLegacy)
        {
          nonLegacyPaths.Add(path);
        }
      }
    }

    return new OpenApiTestData(
      allPaths.OrderBy(p => p).ToArray(),
      nonLegacyPaths.OrderBy(p => p).ToArray());
  }

  private static string NormalizePathTemplate(string pathTemplate)
  {
    if (string.IsNullOrWhiteSpace(pathTemplate))
    {
      return string.Empty;
    }

    var path = pathTemplate.Trim();
    var queryStart = path.IndexOf('?', StringComparison.Ordinal);

    if (queryStart >= 0)
    {
      path = path[..queryStart];
    }

    path = ParameterRegex().Replace(path, "{}");
    path = path.Replace("\\", "/", StringComparison.Ordinal);

    while (path.Contains("//", StringComparison.Ordinal))
    {
      path = path.Replace("//", "/", StringComparison.Ordinal);
    }

    if (!path.StartsWith("/", StringComparison.Ordinal))
    {
      path = $"/{path}";
    }

    return path.ToLowerInvariant();
  }

  [GeneratedRegex("\\{[^{}]+\\}")]
  private static partial Regex ParameterRegex();

  private static string ResolveTemplate(string rawTemplate)
  {
    var resolved = rawTemplate;

    foreach (Match constantMatch in InterpolatedConstantRegex().Matches(rawTemplate))
    {
      var constantName = constantMatch.Groups["name"].Value;
      if (!TryResolveHttpConstantValue(constantName, out var constantValue))
      {
        continue;
      }

      resolved = resolved.Replace(constantMatch.Value, constantValue, StringComparison.Ordinal);
    }

    resolved = InterpolationBlockRegex().Replace(resolved, "{}");

    return resolved;
  }

  private static bool TryGetLongestMatchingConstant(
    string apiPath,
    IReadOnlyCollection<string> constants,
    out string? matchedConstant)
  {
    matchedConstant = constants
      .Where(constant =>
        apiPath.Equals(NormalizePathTemplate(constant), StringComparison.OrdinalIgnoreCase) ||
        apiPath.StartsWith($"{NormalizePathTemplate(constant)}/", StringComparison.OrdinalIgnoreCase))
      .OrderByDescending(constant => constant.Length)
      .FirstOrDefault();

    return matchedConstant is not null;
  }

  private static bool TryResolveHttpConstantValue(string constantName, out string value)
  {
    var parts = constantName.Split('.');
    Type? currentType = typeof(HttpConstants);

    for (var i = 0; i < parts.Length - 1; i++)
    {
      currentType = currentType?.GetNestedType(parts[i], BindingFlags.Public | BindingFlags.Static);
      if (currentType is null)
      {
        value = string.Empty;
        return false;
      }
    }

    var field = currentType?.GetField(parts[^1], BindingFlags.Public | BindingFlags.Static);

    if (field?.GetValue(null) is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
    {
      value = string.Empty;
      return false;
    }

    value = stringValue;
    return true;
  }
}
