using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
///   Marks a test method to be run only on interactive Windows sessions.
/// </summary>
public class InteractiveWindowsFactAttribute : FactAttribute
{
  public InteractiveWindowsFactAttribute()
  {
    if (!OperatingSystem.IsWindows() || !Environment.UserInteractive || IsRunningOnCI())
    {
      Skip = "Test only runs on interactive Windows sessions";
    }
  }

  private static bool IsRunningOnCI()
  {
    return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
  }
}