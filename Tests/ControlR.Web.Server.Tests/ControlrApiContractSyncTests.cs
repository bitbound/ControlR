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
      .Where(path => ApiVersionRegex().IsMatch(path) || path.StartsWith("/api/internal/", StringComparison.OrdinalIgnoreCase))
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

  [GeneratedRegex("HttpConstants\\.(?<name>[A-Za-z0-9_.]+)")]
  private static partial Regex HttpConstantRegex();

  [GeneratedRegex("\\{HttpConstants\\.(?<name>[A-Za-z0-9_.]+)\\}")]
  private static partial Regex InterpolatedConstantRegex();

  [GeneratedRegex("\\{(?<name>[A-Z][A-Za-z0-9_]*)\\}")]
  private static partial Regex InterpolatedIdentRegex();

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
          fileName.StartsWith("V0Api", StringComparison.OrdinalIgnoreCase) ||
          fileName.StartsWith("AgentApi", StringComparison.OrdinalIgnoreCase);
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

  [Fact]
  public void OpenApiActions_AreCoveredByClientMethods()
  {
    var openApiActions = LoadOpenApiActions();
    var clientCalls = LoadClientHttpCalls();

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
  public void ClientMethods_AreBackedByServerActions()
  {
    var openApiActions = LoadOpenApiActions();
    var clientCalls = LoadClientHttpCalls();

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

  private record OpenApiTestData(
    string[] AllPaths,
    string[] NonLegacyPaths);

  private record OpenApiAction(string Verb, string Path, string NormalizedPath);

  private record ClientHttpCall(string Verb, string Path, string NormalizedPath, string MethodName);

  private static readonly HashSet<string> _httpVerbs = new(StringComparer.OrdinalIgnoreCase)
  {
    "Get", "Post", "Put", "Delete", "Patch"
  };

  [GeneratedRegex("\\.(?<verb>GetFromJsonAsync(?:<[^>]+>)?|GetFromJsonAsAsyncEnumerable(?:<[^>]+>)?|GetStringAsync|GetAsync|PostAsJsonAsync(?:<[^>]+>)?|PostAsync|PutAsJsonAsync(?:<[^>]+>)?|PutAsync|DeleteAsync|PatchAsJsonAsync(?:<[^>]+>)?|PatchAsync)\\s*\\(")]
  private static partial Regex DirectHttpCallRegex();

  [GeneratedRegex("new HttpRequestMessage\\s*\\(\\s*HttpMethod\\.(?<verb>Get|Post|Put|Delete|Patch)\\s*,\\s*(?:\\$\"(?<path>[^\"]*)\"|(?<pathconst>HttpConstants\\.[A-Za-z0-9_.]+))")]
  private static partial Regex HttpRequestMessageRegex();

  [GeneratedRegex("HttpConstants\\.(?<name>[A-Za-z0-9_.]+)")]
  private static partial Regex ConstantRefRegex();

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

  private static ClientHttpCall[] LoadClientHttpCalls()
  {
    var repositoryRoot = FindRepositoryRoot();
    var apiClientDirectory = Path.Combine(repositoryRoot, "ControlR.ApiClient", "Implementations");
    var sourceFiles = Directory.EnumerateFiles(apiClientDirectory, "*.cs", SearchOption.TopDirectoryOnly)
      .Where(path =>
      {
        var name = Path.GetFileName(path);
        return name.StartsWith("InternalApi", StringComparison.OrdinalIgnoreCase) ||
          name.StartsWith("V0Api", StringComparison.OrdinalIgnoreCase) ||
          name.StartsWith("AgentApi", StringComparison.OrdinalIgnoreCase);
      });

    var calls = new List<ClientHttpCall>();

    foreach (var sourceFile in sourceFiles)
    {
      var source = File.ReadAllText(sourceFile);
      var localConstants = ExtractLocalStringConstants(source);
      CollectHttpCalls(source, calls, localConstants);
    }

    return calls.ToArray();
  }

  private static Dictionary<string, string> ExtractLocalStringConstants(string source)
  {
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (Match match in LocalConstRegex().Matches(source))
    {
      var name = match.Groups["name"].Value;
      var value = match.Groups["value"].Value.Trim();
      value = ResolveInterpolatedPath(value);
      result[name] = value;
    }

    foreach (Match match in LocalVarAssignmentRegex().Matches(source))
    {
      var name = match.Groups["name"].Value;
      if (result.ContainsKey(name))
      {
        continue;
      }

      var value = match.Groups["value"].Value.Trim();
      value = ResolveInterpolatedPath(value);
      result[name] = value;
    }

    return result;
  }

  [GeneratedRegex("private\\s+const\\s+string\\s+(?<name>[A-Za-z_]\\w*)\\s*=\\s*(?:\\$\"(?<value>[^\"]*)\"|\"(?<value>[^\"]*)\")\\s*;")]
  private static partial Regex LocalConstRegex();

  [GeneratedRegex("\\bvar\\s+(?<name>endpoint|url|path|requestUri|route)\\s*=\\s*(?:\\$\"(?<value>[^\"]*)\"|\"(?<value>[^\"]*)\")\\s*;")]
  private static partial Regex LocalVarAssignmentRegex();

  private static void CollectHttpCalls(string source, List<ClientHttpCall> calls, Dictionary<string, string> localConstants)
  {
    var methodPattern = MethodPatternRegex();
    var directCallPattern = DirectHttpCallRegex();
    var httpRequestMessagePattern = HttpRequestMessageRegex();

    foreach (Match methodMatch in methodPattern.Matches(source))
    {
      var methodName = methodMatch.Groups["name"].Value;
      var startIndex = methodMatch.Index + methodMatch.Length;
      var braceIndex = source.IndexOf('{', startIndex);
      if (braceIndex < 0)
      {
        continue;
      }

      var depth = 1;
      var scanIndex = braceIndex + 1;
      var inString = false;
      var prevWasEscape = false;
      while (scanIndex < source.Length && depth > 0)
      {
        var c = source[scanIndex];
        if (inString)
        {
          if (c == '\\' && !prevWasEscape) { prevWasEscape = true; scanIndex++; continue; }
          if (c == '"' && !prevWasEscape) inString = false;
          prevWasEscape = false;
          scanIndex++;
          continue;
        }

        if (c == '$' && scanIndex + 1 < source.Length && source[scanIndex + 1] == '"')
        {
          inString = true;
          scanIndex += 2;
          continue;
        }

        if (c == '"')
        {
          inString = true;
          scanIndex++;
          continue;
        }

        if (c == '{')
        {
          depth++;
          scanIndex++;
          continue;
        }

        if (c == '}')
        {
          depth--;
          scanIndex++;
          if (depth == 0)
          {
            break;
          }
          continue;
        }

        scanIndex++;
      }

      if (depth != 0)
      {
        continue;
      }

      var body = source.Substring(braceIndex, scanIndex - braceIndex);

      foreach (Match directMatch in directCallPattern.Matches(body))
      {
        var verb = VerbFromCallName(directMatch.Groups["verb"].Value);
        if (verb is null)
        {
          continue;
        }

        var argListStart = directMatch.Index + directMatch.Length;
        var argListEnd = FindMatchingCloseParen(body, argListStart - 1);
        if (argListEnd < 0)
        {
          continue;
        }

        var argList = body.Substring(argListStart, argListEnd - argListStart);
        var path = ExtractFirstPathArgument(argList, localConstants);
        if (path is null)
        {
          continue;
        }

        calls.Add(new ClientHttpCall(
          Verb: verb,
          Path: path,
          NormalizedPath: NormalizePathTemplate(path),
          MethodName: methodName));
      }

      foreach (Match reqMatch in httpRequestMessagePattern.Matches(body))
      {
        var verb = VerbFromHttpMethod(reqMatch.Groups["verb"].Value);
        if (verb is null)
        {
          continue;
        }

        var path = ExtractPathFromHttpRequestMatch(reqMatch);
        if (path is null)
        {
          continue;
        }

        calls.Add(new ClientHttpCall(
          Verb: verb,
          Path: path,
          NormalizedPath: NormalizePathTemplate(path),
          MethodName: methodName));
      }
    }
  }

  [GeneratedRegex("(?:async|public)\\s+(?:Task|IAsyncEnumerable|Task<[^>]+>)[^{]*?\\b(?:I[A-Z]\\w*\\.)(?<name>[A-Z]\\w*)\\s*\\(")]
  private static partial Regex MethodPatternRegex();

  private static string? VerbFromCallName(string methodName)
  {
    if (methodName.StartsWith("Get", StringComparison.Ordinal)) return "GET";
    if (methodName.StartsWith("Post", StringComparison.Ordinal)) return "POST";
    if (methodName.StartsWith("Put", StringComparison.Ordinal)) return "PUT";
    if (methodName.StartsWith("Delete", StringComparison.Ordinal)) return "DELETE";
    if (methodName.StartsWith("Patch", StringComparison.Ordinal)) return "PATCH";
    return null;
  }

  private static readonly string[] _verbNameBlacklist = new[]
  {
    "GetCancellationToken", "GetAwaiter"
  };

  private static string? VerbFromHttpMethod(string methodName) => VerbFromCallName(methodName);

  private static int FindMatchingCloseParen(string text, int openParenIndex)
  {
    var depth = 0;
    var inString = false;
    var prevWasEscape = false;
    for (var i = openParenIndex; i < text.Length; i++)
    {
      var c = text[i];
      if (inString)
      {
        if (c == '\\' && !prevWasEscape) { prevWasEscape = true; continue; }
        if (c == '"' && !prevWasEscape) inString = false;
        prevWasEscape = false;
        continue;
      }

      if (c == '"') { inString = true; continue; }
      if (c == '(') depth++;
      else if (c == ')')
      {
        depth--;
        if (depth == 0) return i;
      }
    }
    return -1;
  }

  private static string? ExtractFirstPathArgument(string argList, Dictionary<string, string> localConstants)
  {
    argList = argList.TrimStart();

    if (argList.StartsWith('$'))
    {
      var quoteStart = argList.IndexOf('"');
      if (quoteStart < 0) return null;
      var endQuote = FindEndOfInterpolatedString(argList, quoteStart);
      if (endQuote < 0) return null;
      var raw = argList.Substring(quoteStart + 1, endQuote - quoteStart - 1);
      return ResolveInterpolatedPath(raw, localConstants);
    }

    if (argList.StartsWith('"'))
    {
      var endQuote = FindEndOfInterpolatedString(argList, 0);
      if (endQuote < 0) return null;
      return argList.Substring(1, endQuote - 1);
    }

    var constMatch = ConstantRefRegex().Match(argList);
    if (constMatch.Success)
    {
      var constantName = constMatch.Groups["name"].Value;
      return TryResolveHttpConstantValue(constantName, out var value) ? value : null;
    }

    var identMatch = System.Text.RegularExpressions.Regex.Match(argList, "^(?<name>[A-Za-z_]\\w*)");
    if (identMatch.Success)
    {
      var name = identMatch.Groups["name"].Value;
      if (localConstants.TryGetValue(name, out var localValue))
      {
        return ResolveInterpolatedPath(localValue, localConstants);
      }
    }

    return null;
  }

  private static int FindEndOfInterpolatedString(string text, int openQuoteIndex)
  {
    var prevWasEscape = false;
    for (var i = openQuoteIndex + 1; i < text.Length; i++)
    {
      var c = text[i];
      if (c == '\\' && !prevWasEscape) { prevWasEscape = true; continue; }
      if (c == '"' && !prevWasEscape) return i;
      prevWasEscape = false;
    }
    return -1;
  }

  private static string? ExtractPathFromHttpRequestMatch(Match match)
  {
    var pathGroup = match.Groups["path"];
    if (pathGroup.Success && pathGroup.Length > 0)
    {
      return ResolveInterpolatedPath(pathGroup.Value);
    }

    var constGroup = match.Groups["pathconst"];
    if (constGroup.Success && constGroup.Length > 0)
    {
      var constantName = constGroup.Value.Substring("HttpConstants.".Length);
      return TryResolveHttpConstantValue(constantName, out var value) ? value : null;
    }

    return null;
  }

  private static string ResolveInterpolatedPath(string raw)
  {
    return ResolveInterpolatedPath(raw, localConstants: null);
  }

  private static string ResolveInterpolatedPath(string raw, Dictionary<string, string>? localConstants)
  {
    var interpolated = InterpolatedConstantRegex().Replace(raw, match =>
    {
      var constantName = match.Groups["name"].Value;
      return TryResolveHttpConstantValue(constantName, out var value) ? value : match.Value;
    });

    if (localConstants is not null)
    {
      interpolated = InterpolatedIdentRegex().Replace(interpolated, match =>
      {
        var name = match.Groups["name"].Value;
        return localConstants.TryGetValue(name, out var value) ? value : match.Value;
      });
    }

    return interpolated;
  }
}
