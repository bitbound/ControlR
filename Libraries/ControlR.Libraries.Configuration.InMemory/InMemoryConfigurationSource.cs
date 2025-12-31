using Microsoft.Extensions.Configuration;

namespace ControlR.Libraries.Configuration.InMemory;

/// <summary>
/// Configuration source that creates and owns an in-memory configuration provider.
/// </summary>
internal class InMemoryConfigurationSource : IConfigurationSource
{
  private readonly InMemoryConfigurationProvider _provider = new();

  /// <summary>
  /// Gets the in-memory configuration provider that can be updated at runtime.
  /// </summary>
  public InMemoryConfigurationProvider Provider => _provider;

  public IConfigurationProvider Build(IConfigurationBuilder builder)
  {
    return _provider;
  }
}
