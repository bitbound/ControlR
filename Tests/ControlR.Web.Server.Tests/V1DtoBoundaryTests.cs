using System.Reflection;
using System.Runtime.CompilerServices;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Services.LogonTokens;
using ControlR.Web.Server.Services.Settings;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Meta-test: V1 DTOs are the stable public contract, so they may only be named by the V1
/// controllers and by the converters that map business models onto them. A service, manager, hub,
/// or background worker that puts a V1 DTO on its signature couples the stable contract to the
/// internal layer, and a contract change then ripples into code that has no business knowing about
/// it. The compiler cannot see this rule (a V1 DTO is just a public type in a referenced
/// assembly), so it is enforced here.
/// Scanned surface: every non-exempt type in the server assembly, and its declared members'
/// return types, parameter types, property types, and field types — recursed through generic
/// arguments and array element types, because the usual shape is
/// <c>Task&lt;SomeV1Dto&gt;</c> rather than a bare <c>SomeV1Dto</c>.
/// Known gaps: a V1 DTO used only inside a method body (a local or an object initializer) is
/// invisible to reflection, and so is one reached through <c>dynamic</c>. Constructor parameters
/// are out of scope, per the plan's enumeration. The signature surface is where the coupling
/// actually hurts, so that is what is covered.
/// </summary>
public class V1DtoBoundaryTests
{
  private const string V1DtoNamespacePrefix = "ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1";

  /// <summary>
  /// The only locations in the server assembly allowed to name a V1 DTO: the V1 controllers and the
  /// V1 converter extensions. Adding a prefix here is a deliberate decision to widen the boundary,
  /// not an incidental edit — <see cref="ExemptPrefixes_DoNotSwallowTheServiceLayer"/> fails if a
  /// prefix grows broad enough to exempt the very types this rule exists to police.
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
    // A guardrail that exempts its own subject passes vacuously. The service-layer types this rule
    // was written for must remain inside the scan, and the scan must find real members on them.
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
      "V1 DTOs are the stable public contract and must stay behind the V1 boundary. Move the V1 " +
      "mapping into ControlR.Web.Server.Api.V1 or ControlR.Web.Server.Extensions.Dtos.V1, and have " +
      "the interior use InternalDtos or a business model. Offenders:\n" +
      string.Join("\n", violations));
  }

  /// <summary>
  /// Maps each scanned type to the number of members inspected on it. Kept as a dictionary so the
  /// vacuity guard can assert per-type coverage instead of trusting an aggregate count.
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
    // NonPublic is included because the private converter on UserPreferencesManager is one of the
    // exact defects this rule names. BindingFlags cannot select internal without private.
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
  /// Exact-or-child matching, so <c>ControlR.Web.Server.Api.V1x</c> cannot be swept in by the
  /// <c>ControlR.Web.Server.Api.V1</c> prefix.
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
    // Constructors are out of scope per the plan's enumeration, and they never even reach here:
    // .ctor carries IsSpecialName and is already dropped by HasSpecialName. Widening to cover
    // constructor parameters would require exempting constructors in that filter as well.
    return member switch
    {
      MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(param => param.ParameterType)],
      PropertyInfo property => [property.PropertyType],
      FieldInfo field => [field.FieldType],
      _ => [],
    };
  }
}
