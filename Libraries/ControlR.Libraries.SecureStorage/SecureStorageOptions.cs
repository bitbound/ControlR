using System.Text.RegularExpressions;

namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Configuration options for secure storage.
/// </summary>
public partial class SecureStorageOptions
{
  private string _serviceName = "ControlR";

  /// <summary>
  /// Gets or sets the service name used for secure storage.
  /// Service name must contain only alphanumeric characters (no spaces or special characters).
  /// </summary>
  public string ServiceName
  {
    get => _serviceName;
    set
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        throw new ArgumentException("Service name cannot be null or whitespace.", nameof(value));
      }

      if (!ServiceNameRegex().IsMatch(value))
      {
        throw new ArgumentException(
          "Service name must contain only alphanumeric characters (no spaces or special characters).",
          nameof(value));
      }

      _serviceName = value;
    }
  }

  [GeneratedRegex("^[a-zA-Z0-9]+$")]
  private static partial Regex ServiceNameRegex();
}
