using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Tests;

public class DesktopPathProviderTests
{
  [Fact]
  public void CreateTemporaryProvider_ResolvesTheSettingsStoreForTheGivenInstance()
  {
    using var provider = StaticServiceProvider.CreateTemporaryProvider("inst-1");

    Assert.Contains("inst-1", provider.GetRequiredService<IDesktopPathProvider>().GetSettingsPath());
    Assert.NotNull(provider.GetService<IDesktopSettingsStore>());
  }

  [Fact]
  public void GetLegacySettingsDirectory_NamesTheVendorlessDirectoryForTheConfiguredInstance()
  {
    var provider = new DesktopPathProvider(
      Options.Create(new DesktopClientOptions { InstanceId = "inst-1" }));

    var legacyDirectory = provider.GetLegacySettingsDirectory();

    // TODO: Temporary migration code for v0.31.x. This is the directory an older client wrote, which is
    // the one shape with no publisher directory in it.
    Assert.Contains("inst-1", legacyDirectory, StringComparison.Ordinal);
    Assert.DoesNotContain("bitbound", legacyDirectory, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void GetSettingsPath_UsesTheConfiguredInstanceId()
  {
    var provider = new DesktopPathProvider(
      Options.Create(new DesktopClientOptions { InstanceId = "inst-1" }));

    Assert.Contains("inst-1", provider.GetSettingsPath());
  }
}
