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
  public void GetLegacySettingsPath_NamesTheVendorlessDirectoryForTheConfiguredInstance()
  {
    var provider = new DesktopPathProvider(
      Options.Create(new DesktopClientOptions { InstanceId = "inst-1" }));

    var legacyPath = provider.GetLegacySettingsPath();

    // TODO: Temporary migration code for v0.31.x. This is the path an older client wrote, which is the
    // one shape without a publisher directory in it.
    Assert.EndsWith("settings.json", legacyPath, StringComparison.Ordinal);
    Assert.DoesNotContain("bitbound", legacyPath, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void GetSettingsPath_UsesTheConfiguredInstanceId()
  {
    var provider = new DesktopPathProvider(
      Options.Create(new DesktopClientOptions { InstanceId = "inst-1" }));

    Assert.Contains("inst-1", provider.GetSettingsPath());
  }
}
