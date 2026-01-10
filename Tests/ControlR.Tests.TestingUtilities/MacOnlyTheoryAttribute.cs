using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
/// Xunit attribute to skip tests when not running on Mac.
/// </summary>
public class MacOnlyTheoryAttribute : TheoryAttribute
{
  public MacOnlyTheoryAttribute()
  {
    if (!OperatingSystem.IsMacOS())
    {
      Skip = "Test only runs on MacOS";
    }
  }
}