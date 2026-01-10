using Xunit;

namespace ControlR.Tests.TestingUtilities;

/// <summary>
/// Xunit attribute to skip tests when not running on Linux.
/// </summary>
public class LinuxOnlyFactAttribute : FactAttribute
{
  public LinuxOnlyFactAttribute()
  {
    if (!OperatingSystem.IsLinux())
    {
      Skip = "Test only runs on Linux";
    }
  }
}