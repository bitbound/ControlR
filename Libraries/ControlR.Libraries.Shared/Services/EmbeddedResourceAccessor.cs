using System.Reflection;
namespace ControlR.Libraries.Shared.Services;

public interface IEmbeddedResourceAccessor
{
  /// <summary>
  /// Gets the content of an embedded resource as a string from the specified assembly asynchronously.
  /// </summary>
  /// <param name="assembly">The assembly to search for the resource.</param>
  /// <param name="resourceName">The name of the embedded resource.</param>
  /// <returns>The content of the embedded resource as a string.</returns>
  Task<string> GetResourceAsString(Assembly assembly, string resourceName);

  /// <summary>
  /// Gets the content of an embedded resource as a byte array from the specified assembly asynchronously.
  /// </summary>
  /// <param name="assembly">The assembly to search for the resource.</param>
  /// <param name="resourceName">The name of the embedded resource.</param>
  /// <returns>The content of the embedded resource as a byte array.</returns>
  Task<byte[]> GetResourceAsBytes(Assembly assembly, string resourceName);
}

public class EmbeddedResourceAccessor : IEmbeddedResourceAccessor
{
  public async Task<string> GetResourceAsString(Assembly assembly, string resourceName)
  {
    await using var stream = GetResourceStream(assembly, resourceName);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
  }

  public async Task<byte[]> GetResourceAsBytes(Assembly assembly, string resourceName)
  {
    await using var stream = GetResourceStream(assembly, resourceName);
    await using var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream);
    return memoryStream.ToArray();
  }

  private static Stream GetResourceStream(Assembly assembly, string resourceName)
  {
    var resourcePath = assembly.GetManifestResourceNames()
      .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
      ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'.");
      
    return assembly.GetManifestResourceStream(resourcePath)
           ?? throw new InvalidOperationException($"Failed to get stream for embedded resource '{resourceName}' in assembly '{assembly.FullName}'.");
  }
}