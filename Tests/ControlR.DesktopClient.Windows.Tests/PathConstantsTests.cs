using System.Runtime.Versioning;
using ControlR.Libraries.TestingUtilities;

namespace ControlR.DesktopClient.Windows.Tests;

[SupportedOSPlatform("windows8.0")]
public class PathConstantsTests
{
  [WindowsOnlyFact]
  public void GetLogsPath_WhenInstanceIdMissing_UsesDefaultDirectory()
  {
    var result = PathConstants.GetLogsPath(instanceId: null);

    Assert.Contains(@"\ControlR\default\", result, StringComparison.OrdinalIgnoreCase);
    Assert.EndsWith(@"\DesktopClient\Logs\LogFile.log", result, StringComparison.OrdinalIgnoreCase);
    // The signed-in user's directory sits between the instance id and the client directory.
    Assert.DoesNotContain(@"default\DesktopClient", result, StringComparison.OrdinalIgnoreCase);
  }

  [WindowsOnlyFact]
  public void GetSettingsPath_WhenInstanceIdIsNotAValidDirectoryName_KeepsItAsWritten()
  {
    var result = PathConstants.GetSettingsPath("inst:1");

    Assert.Contains(@"\inst:1\", result, StringComparison.Ordinal);
    Assert.EndsWith(@"\DesktopClient\settings.json", result, StringComparison.OrdinalIgnoreCase);
  }
}
