using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
///   Marks a test as requiring macOS Keychain availability.
///   On macOS CI runners, Keychain interaction is frequently disallowed (for example OSStatus -25308),
///   so these tests are skipped to avoid false negatives.
/// </summary>
public class MacKeychainIntegrationFactAttribute : FactAttribute
{
  public MacKeychainIntegrationFactAttribute()
  {
    if (!OperatingSystem.IsMacOS())
    {
      return;
    }

    if (!Environment.UserInteractive || IsRunningOnCI())
    {
      Skip = "Test requires interactive macOS Keychain access";
    }
  }

  private static bool IsRunningOnCI()
  {
    return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
  }
}
