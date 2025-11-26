using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
///   Marks a test method to be run only on interactive Windows sessions.
/// </summary>
public class InteractiveWindowsFact : FactAttribute
{
  public InteractiveWindowsFact()
  {
    if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
    {
      Skip = "Test only runs on interactive Windows sessions";
    }
  }
}