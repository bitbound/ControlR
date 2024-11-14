using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

namespace ControlR.Web.Server.Extensions;

public static class PropertyValuesExtensions
{
  private static readonly BindingFlags _bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
  private static readonly ConcurrentDictionary<Type, ImmutableDictionary<string, PropertyInfo>> _propertiesCache = [];

  public static void SetValuesExcept<TDto>(
    this PropertyValues values,
    TDto dto,
    params string[] excludeProperties)
    where TDto : notnull
  {
    var dtoProps = _propertiesCache.GetOrAdd(typeof(TDto), t =>
    {
      return t
        .GetProperties(_bindingFlags)
        .ToImmutableDictionary(x => x.Name);
    });

    foreach (var property in values.Properties)
    {
      if (excludeProperties.Contains(property.Name))
      {
        continue;
      }

      if (dtoProps.TryGetValue(property.Name, out var propInfo))
      {
        values[property.Name] = propInfo.GetValue(dto);
      }
    }
  }
}
