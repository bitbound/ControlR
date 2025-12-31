using Microsoft.Extensions.Configuration;

namespace ControlR.Libraries.Configuration.InMemory;

/// <summary>
/// Configuration provider that stores values in memory and supports runtime updates.
/// </summary>
internal class InMemoryConfigurationProvider : ConfigurationProvider
{
  /// <summary>
  /// Sets a single configuration value and triggers a reload.
  /// </summary>
  public void SetValue(string key, string? value)
  {
    if (value is null)
    {
      Data.Remove(key);
    }
    else
    {
      Data[key] = value;
    }

    OnReload();
  }

  /// <summary>
  /// Sets multiple configuration values and triggers a single reload.
  /// </summary>
  public void SetValues(IEnumerable<KeyValuePair<string, string?>> values)
  {
    foreach (var kvp in values)
    {
      if (kvp.Value is null)
      {
        Data.Remove(kvp.Key);
      }
      else
      {
        Data[kvp.Key] = kvp.Value;
      }
    }

    OnReload();
  }
}
