namespace ControlR.Libraries.Configuration.InMemory;

/// <summary>
/// Provides methods to update in-memory configuration values at runtime.
/// Changes trigger IOptionsMonitor to reload updated configuration sections.
/// </summary>
public interface IInMemoryConfigurationAccessor
{
  /// <summary>
  /// Sets a single configuration value and triggers a reload.
  /// </summary>
  /// <param name="key">The configuration key (e.g., "Section:Property").</param>
  /// <param name="value">The configuration value, or null to remove the key.</param>
  void SetValue(string key, string? value);

  /// <summary>
  /// Sets multiple configuration values and triggers a single reload.
  /// </summary>
  /// <param name="values">The key-value pairs to set. Null values remove keys.</param>
  void SetValues(IEnumerable<KeyValuePair<string, string?>> values);
}

internal class InMemoryConfigurationAccessor(InMemoryConfigurationProvider provider) : IInMemoryConfigurationAccessor
{
  public void SetValue(string key, string? value)
  {
    provider.SetValue(key, value);
  }

  public void SetValues(IEnumerable<KeyValuePair<string, string?>> values)
  {
    provider.SetValues(values);
  }
}
