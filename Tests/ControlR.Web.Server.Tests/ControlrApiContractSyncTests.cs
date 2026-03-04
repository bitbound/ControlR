using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Web.Server.Tests;

public partial class ControlrApiContractSyncTests
{
  [Fact]
  public void HttpConstants_AreExplicitlyMappedToIControlrApiSubClients()
  {
    var endpointPropertyMap = CreateEndpointPropertyMap();
    var constantValues = GetHttpConstantValues();

    var actualPropertyNames = typeof(ControlR.ApiClient.IControlrApi)
      .GetProperties(BindingFlags.Instance | BindingFlags.Public)
      .Select(property => property.Name)
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
    var apiPaths = LoadOpenApiApiPaths();
    var constantValues = GetHttpConstantValues();
    var clientTemplates = LoadClientRouteTemplates(constantValues);

    var missingPaths = apiPaths
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
    var apiPaths = LoadOpenApiApiPaths();
    var constantValues = GetHttpConstantValues();

    foreach (var apiPath in apiPaths)
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
    var apiPaths = LoadOpenApiApiPaths();
    var constantValues = GetHttpConstantValues();
    var endpointPropertyMap = CreateEndpointPropertyMap();
    var actualPropertyNames = typeof(ControlR.ApiClient.IControlrApi)
      .GetProperties(BindingFlags.Instance | BindingFlags.Public)
      .Select(property => property.Name)
      .ToHashSet(StringComparer.Ordinal);

    foreach (var apiPath in apiPaths)
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
        $"Mapped sub-client '{propertyName}' for route '{matchedConstant}' is not present on IControlrApi.");
    }
  }

  private static Dictionary<string, string> CreateEndpointPropertyMap()
  {
    var propertyNames = typeof(ControlR.ApiClient.IControlrApi)
      .GetProperties(BindingFlags.Instance | BindingFlags.Public)
      .Select(property => property.Name)
      .ToHashSet(StringComparer.Ordinal);

    var mappedConstants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var endpointPropertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var endpointOverrides = GetEndpointPropertyOverrides();

    var constantFields = typeof(HttpConstants)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.FieldType == typeof(string))
      .OrderBy(field => field.Name)
      .ToArray();

    foreach (var field in constantFields)
    {
      var constantValue = field.GetValue(null) as string;

      if (string.IsNullOrWhiteSpace(constantValue))
      {
        continue;
      }

      mappedConstants.Add(field.Name);

      var inferredPropertyName = field.Name.EndsWith("Endpoint", StringComparison.Ordinal)
        ? field.Name[..^"Endpoint".Length]
        : field.Name;

      var propertyName = endpointOverrides.TryGetValue(field.Name, out var overridePropertyName)
        ? overridePropertyName
        : inferredPropertyName;

      Assert.True(
        propertyNames.Contains(propertyName),
        $"HttpConstants field '{field.Name}' inferred IControlrApi property '{propertyName}', but that property was not found.");

      endpointPropertyMap[constantValue] = propertyName;
    }

    foreach (var overrideFieldName in endpointOverrides.Keys)
    {
      Assert.True(
        mappedConstants.Contains(overrideFieldName),
        $"Endpoint override is defined for '{overrideFieldName}', but HttpConstants does not contain that field.");
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

  private static Dictionary<string, string> GetEndpointPropertyOverrides()
  {
    return new Dictionary<string, string>(StringComparer.Ordinal)
    {
    };
  }

  private static string[] GetHttpConstantValues()
  {
    var values = typeof(HttpConstants)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.FieldType == typeof(string))
      .Select(field => field.GetValue(null) as string)
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Select(value => value ?? string.Empty)
      .Where(value => value.Length > 0)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return values;
  }

  [GeneratedRegex("HttpConstants\\.(?<name>[A-Za-z0-9_]+)")]
  private static partial Regex HttpConstantRegex();

  [GeneratedRegex("\\{HttpConstants\\.(?<name>[A-Za-z0-9_]+)\\}")]
  private static partial Regex InterpolatedConstantRegex();

  [GeneratedRegex("\\$\\\"(?<value>[^\\\"]*HttpConstants\\.[A-Za-z0-9_]+[^\\\"]*)\\\"")]
  private static partial Regex InterpolatedStringRegex();

  [GeneratedRegex("\\{[^{}]+\\}")]
  private static partial Regex InterpolationBlockRegex();

  private static HashSet<string> LoadClientRouteTemplates(IReadOnlyCollection<string> constantValues)
  {
    var repositoryRoot = FindRepositoryRoot();
    var apiClientDirectory = Path.Combine(repositoryRoot, "ControlR.ApiClient", "Implementations");
    var sourceFiles = Directory
      .EnumerateFiles(apiClientDirectory, "ControlrApi*.cs", SearchOption.TopDirectoryOnly)
      .Where(path => !path.EndsWith("ControlrApi.cs", StringComparison.OrdinalIgnoreCase))
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

  private static string[] LoadOpenApiApiPaths()
  {
    var repositoryRoot = FindRepositoryRoot();
    var openApiPath = Path.Combine(repositoryRoot, "ControlR.Web.Server", "ControlR.Web.Server.json");

    Assert.True(File.Exists(openApiPath), $"OpenAPI file not found: '{openApiPath}'.");

    using var stream = File.OpenRead(openApiPath);
    using var document = JsonDocument.Parse(stream);

    var root = document.RootElement;
    Assert.True(root.TryGetProperty("paths", out var pathsElement), "OpenAPI document does not contain a 'paths' object.");

    var normalizedPaths = pathsElement
      .EnumerateObject()
      .Select(property => property.Name)
      .Where(path => path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
      .Select(NormalizePathTemplate)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(path => path)
      .ToArray();

    return normalizedPaths;
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
    var field = typeof(HttpConstants).GetField(constantName, BindingFlags.Public | BindingFlags.Static);

    if (field?.GetValue(null) is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
    {
      value = string.Empty;
      return false;
    }

    value = stringValue;
    return true;
  }
}
