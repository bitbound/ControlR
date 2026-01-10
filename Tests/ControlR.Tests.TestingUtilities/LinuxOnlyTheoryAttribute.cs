using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
/// Xunit attribute to skip tests when not running on Linux.
/// </summary>
public class LinuxOnlyTheoryAttribute : TheoryAttribute
{
  public LinuxOnlyTheoryAttribute()
  {
    if (!OperatingSystem.IsLinux())
    {
      Skip = "Test only runs on Linux";
    }
  }
}