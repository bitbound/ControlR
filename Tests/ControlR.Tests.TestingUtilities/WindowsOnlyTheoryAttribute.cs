using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
/// Xunit attribute to skip tests when not running on Windows.
/// </summary>
public class WindowsOnlyTheoryAttribute : TheoryAttribute
{
  public WindowsOnlyTheoryAttribute()
  {
    if (!OperatingSystem.IsWindows())
    {
      Skip = "Test only runs on Windows";
    }
  }
}