using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlR.Web.Server.Tests;

public partial class ControlrApiContractSyncTests
{
  private static readonly HashSet<string> _httpVerbs = new(StringComparer.OrdinalIgnoreCase)
  {
    "Get", "Post", "Put", "Delete", "Patch"
  };

  [Fact]
  public void ClientMethods_AreBackedByServerActions()
  {
    var openApiActions = LoadOpenApiActions();
    var clientCalls = LoadClientCallsFromAttributes();

    var extra = clientCalls
      .Where(call => !openApiActions.Any(action =>
        action.Verb == call.Verb &&
        action.NormalizedPath == call.NormalizedPath))
      .OrderBy(call => call.Verb)
      .ThenBy(call => call.NormalizedPath)
      .ToArray();

    var grouped = extra
      .GroupBy(call => call.NormalizedPath)
      .OrderBy(group => group.Key)
      .Select(group => $"{group.Key}: [{string.Join(", ", group.Select(c => c.Verb))}]")
      .ToArray();

    Assert.True(
      extra.Length == 0,
      $"Client methods with no matching server action: {string.Join(" | ", grouped)}");
  }

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
  public void OpenApiActions_AreCoveredByClientMethods()
  {
    var openApiActions = LoadOpenApiActions();
    var clientCalls = LoadClientCallsFromAttributes();

    var missing = openApiActions
      .Where(action => !clientCalls.Any(call =>
        call.Verb == action.Verb &&
        call.NormalizedPath == action.NormalizedPath))
      .OrderBy(action => action.Verb)
      .ThenBy(action => action.Path)
      .ToArray();

    var grouped = missing
      .GroupBy(action => action.Path)
      .OrderBy(group => group.Key)
      .Select(group => $"{group.Key}: [{string.Join(", ", group.Select(a => a.Verb))}]")
      .ToArray();

    Assert.True(
      missing.Length == 0,
      $"Server actions with no matching client method: {string.Join(" | ", grouped)}");
  }

  [Fact]
  public void OpenApiApiPaths_AreCoveredByClientRouteTemplates()
  {
    var apiData = LoadOpenApiTestData();
    var clientTemplates = LoadClientRouteTemplatesFromAttributes();

    var missingPaths = apiData.NonLegacyPaths
      .Where(path => !clientTemplates.Contains(path))
      .Where(path => ApiVersionRegex().IsMatch(path) || path.StartsWith("/api/internal/", StringComparison.OrdinalIgnoreCase))
      .OrderBy(path => path)
      .ToArray();

    Assert.True(
      missingPaths.Length == 0,
      $"Client route templates are missing OpenAPI paths: {string.Join(", ", missingPaths)}");
  }

  [Fact]
  public void OpenApiApiPaths_AreCoveredByHttpConstants()
  {
    var apiData = LoadOpenApiTestData();
    var constantValues = GetHttpConstantValues();

    foreach (var apiPath in apiData.NonLegacyPaths)
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

    foreach (var apiPath in apiData.NonLegacyPaths)
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

  [GeneratedRegex("^/api/v\\d+/")]
  private static partial Regex ApiVersionRegex();

  private static void CollectCallsFromInterface(Type interfaceType, List<ClientCall> calls)
  {
    foreach (var method in interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
    {
      var attr = method.GetCustomAttribute<ApiRouteAttribute>();
      if (attr is null)
      {
        continue;
      }

      calls.Add(new ClientCall(
        Verb: attr.Verb.ToUpperInvariant(),
        Path: attr.RouteTemplate,
        NormalizedPath: NormalizePathTemplate(attr.RouteTemplate),
        MethodName: method.Name));
    }
  }

  private static Dictionary<string, string> CreateEndpointPropertyMap()
  {
    var propertyNames = GetAllSubInterfacePropertyNames();
    var endpointPropertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var field in GetAllHttpConstantFields().OrderBy(field => field.Name))
    {
      if (field.DeclaringType?.Name == "Legacy")
      {
        continue;
      }

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
      typeof(IControlrAgentApi),
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
      .Where(field => field.DeclaringType?.Name != "Legacy")
      .Select(field => field.GetValue(null) as string)
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Select(value => value ?? string.Empty)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return values;
  }

  // IMPORTANT: When a new sub-interface is added (e.g., IControlrXxxApi in
  // ControlR.ApiClient.Interfaces), add it here so the contract sync tests
  // pick up its [ApiRoute] attributes. Forgetting this list silently disables
  // coverage for the entire sub-interface.
  private static Type[] GetSubInterfaceTypes()
  {
    return
    [
      typeof(IControlrInternalApi),
      typeof(IControlrV0Api),
      typeof(IControlrAgentApi),
    ];
  }

  private static ClientCall[] LoadClientCallsFromAttributes()
  {
    var calls = new List<ClientCall>();

    foreach (var subInterfaceType in GetSubInterfaceTypes())
    {
      var subInterfacePropertyNames = subInterfaceType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(p => p.PropertyType)
        .Where(t => t.IsInterface)
        .ToArray();

      foreach (var propertyType in subInterfacePropertyNames)
      {
        CollectCallsFromInterface(propertyType, calls);
      }
    }

    return calls.ToArray();
  }

  private static HashSet<string> LoadClientRouteTemplatesFromAttributes()
  {
    var templates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var subInterfaceType in GetSubInterfaceTypes())
    {
      var subInterfacePropertyNames = subInterfaceType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(p => p.PropertyType)
        .Where(t => t.IsInterface)
        .ToArray();

      foreach (var propertyType in subInterfacePropertyNames)
      {
        foreach (var method in propertyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
          var attr = method.GetCustomAttribute<ApiRouteAttribute>();
          if (attr is null)
          {
            continue;
          }

          var normalized = NormalizePathTemplate(attr.RouteTemplate);
          if (normalized.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
          {
            templates.Add(normalized);
          }
        }
      }
    }

    return templates;
  }

  private static OpenApiAction[] LoadOpenApiActions()
  {
    var repositoryRoot = FindRepositoryRoot();
    var serverDirectory = Path.Combine(repositoryRoot, "ControlR.Web.Server");
    var jsonFiles = Directory.GetFiles(serverDirectory, "ControlR.Web.Server_*.json");

    var legacyPrefixes = GetAllHttpConstantFields()
      .Where(field => field.DeclaringType?.Name == "Legacy")
      .Select(field => NormalizePathTemplate((string)field.GetValue(null)!))
      .Where(prefix => !string.IsNullOrEmpty(prefix))
      .ToArray();

    var actions = new List<OpenApiAction>();

    foreach (var jsonFile in jsonFiles)
    {
      using var stream = File.OpenRead(jsonFile);
      using var document = JsonDocument.Parse(stream);

      if (!document.RootElement.TryGetProperty("paths", out var pathsElement))
      {
        continue;
      }

      foreach (var pathProperty in pathsElement.EnumerateObject())
      {
        if (!pathProperty.Name.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        var normalized = NormalizePathTemplate(pathProperty.Name);
        if (legacyPrefixes.Any(prefix => normalized == prefix || normalized.StartsWith($"{prefix}/", StringComparison.Ordinal)))
        {
          continue;
        }

        foreach (var operationProperty in pathProperty.Value.EnumerateObject())
        {
          var verb = operationProperty.Name.ToUpperInvariant();
          if (!_httpVerbs.Contains(verb))
          {
            continue;
          }

          actions.Add(new OpenApiAction(verb, pathProperty.Name, normalized));
        }
      }
    }

    return actions.ToArray();
  }

  private static OpenApiTestData LoadOpenApiTestData()
  {
    var repositoryRoot = FindRepositoryRoot();
    var serverDirectory = Path.Combine(repositoryRoot, "ControlR.Web.Server");

    var jsonFiles = Directory.GetFiles(serverDirectory, "ControlR.Web.Server_*.json");
    Assert.True(jsonFiles.Length > 0, $"No OpenAPI JSON files found in '{serverDirectory}'.");

    var legacyPrefixes = GetAllHttpConstantFields()
      .Where(field => field.DeclaringType?.Name == "Legacy")
      .Select(field => NormalizePathTemplate((string)field.GetValue(null)!))
      .Where(prefix => !string.IsNullOrEmpty(prefix))
      .ToArray();

    var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var nonLegacyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var jsonFile in jsonFiles)
    {
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

        var isLegacy = legacyPrefixes.Any(prefix =>
          path == prefix ||
          path.StartsWith($"{prefix}/", StringComparison.Ordinal));

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

  private record ClientCall(string Verb, string Path, string NormalizedPath, string MethodName);
  private record OpenApiAction(string Verb, string Path, string NormalizedPath);
  private record OpenApiTestData(
    string[] AllPaths,
    string[] NonLegacyPaths);
}
