using System.Reflection;
using System.Runtime.CompilerServices;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.Settings;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Guards the V1 DTO boundary: only the V1 controllers and the V1 converter extensions may name a
/// V1 DTO. A V1 DTO on any other server signature couples the stable contract to the internal layer.
/// Scans declared member signatures (recursing generic arguments and array elements), not method
/// bodies, so a V1 DTO used only as a local is invisible here.
/// </summary>
public class V1DtoBoundaryTests
{
  private const string V1DtoNamespacePrefix = "ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1";

  /// <summary>
  /// Namespaces allowed to name a V1 DTO. Widening this is a deliberate decision to widen the
  /// boundary; <see cref="ExemptPrefixes_DoNotSwallowTheServiceLayer"/> fails if it swallows the
  /// service layer.
  /// </summary>
  private static readonly string[] _exemptNamespacePrefixes =
  [
    "ControlR.Web.Server.Api.V1",
    "ControlR.Web.Server.Extensions.Dtos.V1",
  ];

  private static Assembly ServerAssembly { get; } = typeof(DeviceResourcePolicies).Assembly;

  [Fact]
  public void ExemptPrefixes_DoNotSwallowTheServiceLayer()
  {
    // A guardrail that exempts its own subject passes vacuously.
    var scanned = CollectScannedSurface();

    var serviceTypesThatMustBeScanned = new[]
    {
      typeof(IUserPreferencesManager),
      typeof(UserPreferencesManager),
      typeof(LogonTokenCreationRequest),
    };

    var uninspected = serviceTypesThatMustBeScanned
      .Where(type => !scanned.TryGetValue(type, out var memberCount) || memberCount == 0)
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      uninspected.Count == 0,
      "These service-layer types fell outside the V1 DTO scan or exposed no inspectable members, " +
      "so the exempt prefixes are too broad or the member surface no longer matches declared " +
      $"code: {string.Join(", ", uninspected)}");
  }

  [Fact]
  public void NoTypeOutsideTheV1Boundary_SurfacesAV1Dto()
  {
    var violations = CollectViolations();

    Assert.True(
      violations.Count == 0,
      "V1 DTOs must stay behind the V1 boundary. Offenders:\n" + string.Join("\n", violations));
  }

  /// <summary>
  /// Maps each scanned type to the number of members inspected, so the vacuity guard can assert
  /// per-type coverage.
  /// </summary>
  private static Dictionary<Type, int> CollectScannedSurface()
  {
    var surface = new Dictionary<Type, int>();

    foreach (var type in ScannedTypes())
    {
      surface[type] = InspectableMembers(type).Count;
    }

    return surface;
  }

  private static List<string> CollectViolations()
  {
    // A sorted set both dedupes (a record's synthetic members restate the same defect) and gives a
    // stable, ordered failure message.
    var report = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var type in ScannedTypes())
    {
      foreach (var v1Type in FindV1Types(type.GetGenericArguments()))
      {
        report.Add($"{type.FullName}.<generic-arguments> surfaces {v1Type.FullName}");
      }

      foreach (var member in InspectableMembers(type))
      {
        foreach (var v1Type in FindV1Types(SurfaceTypes(member)))
        {
          report.Add($"{type.FullName}.{member.Name} surfaces {v1Type.FullName}");
        }
      }
    }

    return [.. report];
  }

  private static IEnumerable<Type> FindV1Types(IEnumerable<Type> roots)
  {
    var visited = new HashSet<Type>();
    var pending = new Queue<Type>(roots);

    while (pending.Count > 0)
    {
      var current = pending.Dequeue();
      if (!visited.Add(current))
      {
        continue;
      }

      if (IsV1DtoNamespaceType(current))
      {
        yield return current;
      }

      if (current.HasElementType && current.GetElementType() is { } elementType)
      {
        pending.Enqueue(elementType);
      }

      foreach (var genericArgument in current.GetGenericArguments())
      {
        pending.Enqueue(genericArgument);
      }
    }
  }

  private static bool HasSpecialName(MemberInfo member)
  {
    return member switch
    {
      MethodBase method => method.IsSpecialName,
      PropertyInfo property => property.IsSpecialName,
      FieldInfo field => field.IsSpecialName,
      EventInfo evt => evt.IsSpecialName,
      _ => false,
    };
  }

  private static List<MemberInfo> InspectableMembers(Type type)
  {
    // NonPublic is included because a private converter is one of the defects this rule names.
    // BindingFlags cannot select internal without private.
    return type
      .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
      // Accessors and compiler synthetics would restate their property or method.
      .Where(member => !HasSpecialName(member))
      .Where(member => !member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
      .ToList();
  }

  private static bool IsCompilerGenerated(Type type)
  {
    return type.Name.Contains('<', StringComparison.Ordinal) || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
  }

  private static bool IsExempt(Type type)
  {
    return _exemptNamespacePrefixes.Any(prefix => NamespaceMatches(type.Namespace, prefix));
  }

  private static bool IsV1DtoNamespaceType(Type type)
  {
    return NamespaceMatches(type.Namespace, V1DtoNamespacePrefix);
  }

  /// <summary>
  /// Exact-or-child matching, so <c>...Api.V1x</c> is not swept in by the <c>...Api.V1</c> prefix.
  /// </summary>
  private static bool NamespaceMatches(string? candidate, string prefix)
  {
    return candidate == prefix || candidate?.StartsWith(prefix + ".", StringComparison.Ordinal) == true;
  }

  private static IEnumerable<Type> ScannedTypes()
  {
    return ServerAssembly
      .GetTypes()
      .Where(type => !IsCompilerGenerated(type))
      .Where(type => !IsExempt(type));
  }

  private static IEnumerable<Type> SurfaceTypes(MemberInfo member)
  {
    // Constructors never reach here: .ctor carries IsSpecialName and is dropped by HasSpecialName.
    return member switch
    {
      MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(param => param.ParameterType)],
      PropertyInfo property => [property.PropertyType],
      FieldInfo field => [field.FieldType],
      _ => [],
    };
  }
}
