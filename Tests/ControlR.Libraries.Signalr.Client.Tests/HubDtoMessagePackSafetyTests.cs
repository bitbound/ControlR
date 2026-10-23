using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using MessagePack;

namespace ControlR.Libraries.Signalr.Client.Tests;

/// <summary>
/// Inventories every type reachable from a hub interface and asserts each one is MessagePack-aware.
/// A hub DTO with no such annotation serializes under MessagePack as a bare array of its members,
/// which a peer reading the same shape by member name cannot decode.
/// </summary>
public class HubDtoMessagePackSafetyTests
{
  private static readonly Type[] _hubDeclarations =
  [
    typeof(IAgentHub),
    typeof(IViewerHub),
    typeof(IAgentHubClient),
    typeof(IViewerHubClient),
  ];

  private static readonly HashSet<Type> _scalarTypes =
  [
    typeof(string),
    typeof(bool),
    typeof(byte),
    typeof(sbyte),
    typeof(short),
    typeof(ushort),
    typeof(int),
    typeof(uint),
    typeof(long),
    typeof(ulong),
    typeof(float),
    typeof(double),
    typeof(decimal),
    typeof(char),
    typeof(Guid),
    typeof(DateTime),
    typeof(DateTimeOffset),
    typeof(TimeSpan),
    typeof(DateOnly),
    typeof(TimeOnly),
    typeof(Uri),
    typeof(Version),
  ];

  private static readonly HashSet<Type> _wrapperDefinitions =
  [
    typeof(IReadOnlyList<>),
    typeof(IReadOnlyCollection<>),
    typeof(List<>),
    typeof(IEnumerable<>),
    typeof(ImmutableArray<>),
    typeof(IAsyncEnumerable<>),
  ];

  public static TheoryData<Type> ReachablePayloadTypes
  {
    get
    {
      var data = new TheoryData<Type>();
      foreach (var type in EnumeratePayloadTypes())
      {
        data.Add(type);
      }
      return data;
    }
  }

  [Theory]
  [MemberData(nameof(ReachablePayloadTypes))]
  public void EveryReachablePayloadTypeCarriesMessagePackObject(Type payloadType)
  {
    Assert.True(
      payloadType.GetCustomAttribute<MessagePackObjectAttribute>(inherit: false) is not null,
      $"'{payloadType.FullName}' is reachable from a hub interface but carries no [MessagePackObject] attribute.");
  }

  private static IEnumerable<Type> EnumeratePayloadTypes()
  {
    var visited = new HashSet<Type>();
    var pending = new Queue<Type>();

    foreach (var declaration in _hubDeclarations)
    {
      foreach (var method in declaration.GetMethods())
      {
        foreach (var parameter in method.GetParameters())
        {
          pending.Enqueue(parameter.ParameterType);
        }
        pending.Enqueue(method.ReturnType);
      }
    }

    while (pending.Count > 0)
    {
      var candidate = StripContainer(pending.Dequeue());
      if (candidate is null || !visited.Add(candidate))
      {
        continue;
      }

      if (TryGetContainedTypes(candidate, out var contained))
      {
        foreach (var type in contained)
        {
          pending.Enqueue(type);
        }
        continue;
      }

      if (candidate.IsEnum || _scalarTypes.Contains(candidate))
      {
        continue;
      }

      yield return candidate;

      foreach (var child in GetChildTypes(candidate))
      {
        pending.Enqueue(child);
      }
    }
  }

  private static IEnumerable<Type> GetChildTypes(Type type)
  {
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      yield return property.PropertyType;
    }

    foreach (var constructor in type.GetConstructors())
    {
      foreach (var parameter in constructor.GetParameters())
      {
        yield return parameter.ParameterType;
      }
    }
  }

  private static Type? StripContainer(Type type)
  {
    while (true)
    {
      if (type == typeof(Task))
      {
        return null;
      }

      if (!type.IsGenericType)
      {
        return type;
      }

      var definition = type.GetGenericTypeDefinition();
      if (definition == typeof(Task<>))
      {
        type = type.GetGenericArguments()[0];
        continue;
      }

      if (definition == typeof(ChannelReader<>))
      {
        type = type.GetGenericArguments()[0];
        continue;
      }

      return type;
    }
  }

  private static bool TryGetContainedTypes(Type type, out Type[] contained)
  {
    if (type.IsArray && type.GetElementType() is { } elementType)
    {
      contained = [elementType];
      return true;
    }

    if (Nullable.GetUnderlyingType(type) is { } underlying)
    {
      contained = [underlying];
      return true;
    }

    if (type.IsGenericType && _wrapperDefinitions.Contains(type.GetGenericTypeDefinition()))
    {
      contained = type.GetGenericArguments();
      return true;
    }

    contained = [];
    return false;
  }
}