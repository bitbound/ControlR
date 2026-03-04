using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ControlR.ApiClient;

internal static class DtoValidatorFactory
{
  private const string ContractsDtoNamespacePrefix = "ControlR.Libraries.Api.Contracts.Dtos";
  private const int MaxDepth = 32;

  private static readonly ConcurrentDictionary<Type, TypeValidationPlan> _cache = new();

  public static string? Validate(object? dto)
  {
    if (dto is null)
    {
      return "DTO instance is null.";
    }

    var type = dto.GetType();
    if (!ShouldValidateType(type) || IsTerminalType(type))
    {
      return null;
    }

    var errors = new List<string>();
    var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
    ValidateObject(dto, errors, string.Empty, 0, visited);

    return errors.Count == 0 ? null : string.Join(", ", errors);
  }

  private static TypeValidationPlan BuildValidator(Type type)
  {
    if (!ShouldValidateType(type) || IsTerminalType(type))
    {
      return new TypeValidationPlan([]);
    }

    var nullabilityCtx = new NullabilityInfoContext();
    var accessors = new List<PropertyMeta>();

    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      if (!prop.CanRead)
      {
        continue;
      }

      var propType = prop.PropertyType;
      var nullability = nullabilityCtx.Create(prop);
      var isRequired = prop.IsDefined(typeof(RequiredAttribute), true)
                       || prop.IsDefined(typeof(RequiredMemberAttribute), true)
                       || (!propType.IsValueType &&
                           nullability.ReadState == NullabilityState.NotNull);

      var isCollection = propType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propType);
      var hasNonNullableCollectionElements = isCollection &&
                                             nullability.ElementType?.ReadState == NullabilityState.NotNull;

      var param = Expression.Parameter(typeof(object), "o");
      var getterBody = Expression.Convert(Expression.Property(Expression.Convert(param, type), prop), typeof(object));
      var getter = Expression.Lambda<Func<object, object?>>(getterBody, param).Compile();

      accessors.Add(new PropertyMeta(
        prop.Name,
        isRequired,
        isCollection,
        hasNonNullableCollectionElements,
        getter));
    }

    return new TypeValidationPlan(accessors);
  }

  private static bool IsTerminalType(Type t)
  {
    if (t == typeof(string)) return true;
    if (t.IsPrimitive) return true;
    if (t.IsEnum) return true;
    if (t == typeof(decimal)) return true;
    if (t == typeof(DateTime)) return true;
    if (t == typeof(DateTimeOffset)) return true;
    if (t == typeof(TimeSpan)) return true;
    if (t == typeof(DateOnly)) return true;
    if (t == typeof(TimeOnly)) return true;
    if (t == typeof(Guid)) return true;
    if (t == typeof(Uri)) return true;
    if (t == typeof(Version)) return true;
    if (t == typeof(byte[])) return true;

    if (Nullable.GetUnderlyingType(t) is Type underlying)
    {
      return IsTerminalType(underlying);
    }

    return false;
  }

  private static bool ShouldValidateType(Type type)
  {
    var ns = type.Namespace;
    if (string.IsNullOrWhiteSpace(ns))
    {
      return false;
    }

    return ns.StartsWith(ContractsDtoNamespacePrefix, StringComparison.Ordinal);
  }

  private static void ValidateObject(
    object dto,
    List<string> errors,
    string currentPath,
    int depth,
    HashSet<object> visited)
  {
    var type = dto.GetType();
    if (!ShouldValidateType(type) || IsTerminalType(type))
    {
      return;
    }

    if (depth >= MaxDepth)
    {
      var path = string.IsNullOrWhiteSpace(currentPath) ? "<root>" : currentPath;
      errors.Add($"{path}.<max-depth>");
      return;
    }

    if (!type.IsValueType && !visited.Add(dto))
    {
      return;
    }

    var plan = _cache.GetOrAdd(type, BuildValidator);
    foreach (var property in plan.Properties)
    {
      var value = property.Getter(dto);
      var path = string.IsNullOrWhiteSpace(currentPath)
        ? property.Name
        : $"{currentPath}.{property.Name}";

      if (value is null)
      {
        if (property.IsRequired)
        {
          errors.Add(path);
        }

        continue;
      }

      if (!property.IsCollection)
      {
        ValidateObject(value, errors, path, depth + 1, visited);
        continue;
      }

      var index = 0;
      foreach (var item in (IEnumerable)value)
      {
        var itemPath = $"{path}[{index}]";

        if (item is null)
        {
          if (property.HasNonNullableCollectionElements)
          {
            errors.Add(itemPath);
          }

          index++;
          continue;
        }

        ValidateObject(item, errors, itemPath, depth + 1, visited);
        index++;
      }
    }
  }

  private sealed record PropertyMeta(
    string Name,
    bool IsRequired,
    bool IsCollection,
    bool HasNonNullableCollectionElements,
    Func<object, object?> Getter);
  private sealed record TypeValidationPlan(IReadOnlyList<PropertyMeta> Properties);
}
