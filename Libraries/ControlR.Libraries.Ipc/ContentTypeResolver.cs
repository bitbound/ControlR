using System;
using System.Collections.Concurrent;
using System.Linq;

namespace ControlR.Libraries.Ipc;

public interface IContentTypeResolver
{
  /// <summary>
  /// Resolves a type from its name, ignoring assembly version information.
  /// </summary>
  Type? ResolveType(string typeName);
}

internal class ContentTypeResolver : IContentTypeResolver
{
  private readonly ConcurrentDictionary<string, Type?> _typeCache = new();

  public Type? ResolveType(string typeName)
  {
    if (string.IsNullOrEmpty(typeName))
      return null;

    return _typeCache.GetOrAdd(typeName, ResolveTypeFromName);
  }

  /// <summary>
  /// Resolves a type from its name, ignoring assembly version information.
  /// </summary>
  private static Type? ResolveTypeFromName(string typeName)
  {
    if (string.IsNullOrEmpty(typeName))
      return null;

    // First try direct resolution
    var type = Type.GetType(typeName, false);
    if (type != null)
      return type;

    // If that fails, search through all loaded assemblies
    return AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(assembly => 
      {
        try 
        { 
          return assembly.GetTypes(); 
        }
        catch 
        { 
          return Array.Empty<Type>(); 
        }
      })
      .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
  }
}
