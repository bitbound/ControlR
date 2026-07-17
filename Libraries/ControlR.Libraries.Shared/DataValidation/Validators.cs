using ControlR.Libraries.Shared.Constants;

namespace ControlR.Libraries.Shared.DataValidation;

public static class Validators
{
  public static bool IsReservedInstanceId(string instanceId)
  {
    return string.Equals(instanceId.Trim(), AppConstants.DefaultInstanceId, StringComparison.OrdinalIgnoreCase);
  }

  public static bool ValidateDisplayName(string displayName, out char[] illegalCharacters)
  {
    illegalCharacters =
    [
      .. displayName
        .Where(c => !char.IsLetterOrDigit(c) && c is not ' ' and not '_' and not '-')
        .Distinct()
    ];

    return illegalCharacters.Length == 0;
  }

  /// <summary>
  /// Validates the instance ID when a non-null, non-whitespace value is provided.
  /// If the instance ID is null or whitespace, no validation is performed and the value is treated as valid.
  /// When validated, the instance ID must not be reserved and must only contain allowed characters.
  /// </summary>
  /// <param name="instanceId">The instance ID to validate, or null/whitespace to skip validation.</param>
  /// <returns>Null if the instance ID is considered valid or not validated, or an error message if it is invalid.</returns>
  public static string? ValidateInstanceId(string? instanceId)
  {
    if (string.IsNullOrWhiteSpace(instanceId))
    {
      return null;
    }

    var trimmedInstanceId = instanceId.Trim();
    if (IsReservedInstanceId(trimmedInstanceId))
    {
      return $"Instance ID '{AppConstants.DefaultInstanceId}' is reserved.";
    }

    if (trimmedInstanceId is "." or "..")
    {
      return "Instance ID cannot be '.' or '..'.";
    }

    if (trimmedInstanceId.Contains(Path.DirectorySeparatorChar) || trimmedInstanceId.Contains(Path.AltDirectorySeparatorChar))
    {
      return "Instance ID must not contain path separators.";
    }

    if (!IsValidInstanceIdCharacters(trimmedInstanceId, out var invalidChars))
    {
      return $"Instance ID contains one or more invalid characters: {string.Join(", ", invalidChars)}";
    }

    return null;
  }

  /// <summary>
  /// Determines whether the instance ID has only the allowed characters: alphanumeric, period, underscore, and hyphen.
  /// </summary>
  private static bool IsValidInstanceIdCharacters(string instanceId, out char[] illegalCharacters)
  {
    illegalCharacters =
    [
      .. instanceId
        .Where(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-')
        .Distinct()
    ];

    return illegalCharacters.Length == 0;
  }

}