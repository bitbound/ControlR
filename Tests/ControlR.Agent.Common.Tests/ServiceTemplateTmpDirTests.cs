using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Shared.Services;
using System.Reflection;

namespace ControlR.Agent.Common.Tests;

public class ServiceTemplateTmpDirTests
{
  private readonly EmbeddedResourceAccessor _accessor = new();
  private readonly Assembly _assembly = typeof(FileSystemPathProvider).Assembly;

  [Theory]
  [InlineData("LaunchDaemon.plist", "<key>TMPDIR</key>")]
  [InlineData("InstallerDaemon.plist", "<key>TMPDIR</key>")]
  [InlineData("controlr.agent.service", "Environment=TMPDIR=/var/tmp")]
  public async Task DaemonTemplates_PinTmpDirForElevatedContexts(string resource, string expectedLine)
  {
    var content = await _accessor.GetResourceAsString(_assembly, resource);

    Assert.Contains(expectedLine, content, StringComparison.Ordinal);
    Assert.Contains("/var/tmp", content, StringComparison.Ordinal);
  }
}
