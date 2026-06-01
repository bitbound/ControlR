namespace ControlR.Libraries.Branding.Tests;

// This test class is silly, but I'll allow it.  At least it will flag unintended changes.
public class BrandingConstantsTests
{
  [Fact]
  public void AgentBaseName_UsesBrandKey()
  {
    Assert.Equal("ControlR.Agent", BrandingConstants.AgentBaseName);
  }

  [Fact]
  public void AuthenticatorIssuerName_EqualsBrandName()
  {
    Assert.Equal(BrandingConstants.BrandName, BrandingConstants.AuthenticatorIssuerName);
  }

  [Fact]
  public void BrandName_IsControlR()
  {
    Assert.Equal("ControlR", BrandingConstants.BrandName);
  }

  [Fact]
  public void BundleHashFileName_PrefixedWithDotLowercase()
  {
    Assert.Equal(".controlr-bundle.sha256", BrandingConstants.BundleHashFileName);
  }

  [Fact]
  public void BundleZipBaseName_UsesBrandKey()
  {
    Assert.Equal("ControlR.Agent.bundle", BrandingConstants.BundleZipBaseName);
  }

  [Theory]
  [InlineData("PrimaryColorDark", "2196F3")]
  [InlineData("SecondaryColorDark", "21f3e9")]
  [InlineData("TertiaryColorDark", "7b21f3")]
  [InlineData("InfoColorDark", "89b4f8")]
  [InlineData("SuccessColorDark", "2cb67d")]
  [InlineData("WarningColorDark", "facc15")]
  [InlineData("ErrorColorDark", "f87171")]
  [InlineData("PrimaryColorLight", "2196F3")]
  [InlineData("SecondaryColorLight", "008c7a")]
  [InlineData("TertiaryColorLight", "7b21f3")]
  [InlineData("InfoColorLight", "0d6efd")]
  [InlineData("SuccessColorLight", "28a745")]
  [InlineData("WarningColorLight", "ffc107")]
  [InlineData("ErrorColorLight", "dc3545")]
  public void ColorConstants_AreValidHexValues(string propertyName, string expectedValue)
  {
    var value = propertyName switch
    {
      "PrimaryColorDark" => BrandingConstants.PrimaryColorDark,
      "SecondaryColorDark" => BrandingConstants.SecondaryColorDark,
      "TertiaryColorDark" => BrandingConstants.TertiaryColorDark,
      "InfoColorDark" => BrandingConstants.InfoColorDark,
      "SuccessColorDark" => BrandingConstants.SuccessColorDark,
      "WarningColorDark" => BrandingConstants.WarningColorDark,
      "ErrorColorDark" => BrandingConstants.ErrorColorDark,
      "PrimaryColorLight" => BrandingConstants.PrimaryColorLight,
      "SecondaryColorLight" => BrandingConstants.SecondaryColorLight,
      "TertiaryColorLight" => BrandingConstants.TertiaryColorLight,
      "InfoColorLight" => BrandingConstants.InfoColorLight,
      "SuccessColorLight" => BrandingConstants.SuccessColorLight,
      "WarningColorLight" => BrandingConstants.WarningColorLight,
      "ErrorColorLight" => BrandingConstants.ErrorColorLight,
      _ => throw new ArgumentException($"Unknown property: {propertyName}")
    };
    Assert.Equal(expectedValue, value);
    Assert.Matches("^[0-9a-fA-F]{6}$", value);
  }

  [Fact]
  public void DesktopClientBaseName_UsesBrandKey()
  {
    Assert.Equal("ControlR.DesktopClient", BrandingConstants.DesktopClientBaseName);
  }

  [Fact]
  public void DesktopClientDirectoryName_IsDesktopClient()
  {
    Assert.Equal("DesktopClient", BrandingConstants.DesktopClientDirectoryName);
  }

  [Fact]
  public void InstallerBaseName_UsesBrandKey()
  {
    Assert.Equal("ControlR.Agent.Installer", BrandingConstants.InstallerBaseName);
  }

  [Fact]
  public void LinuxAgentServiceName_UsesLowercaseBrandKey()
  {
    Assert.Equal("controlr.agent.service", BrandingConstants.LinuxAgentServiceName);
  }

  [Fact]
  public void LinuxDesktopServiceName_UsesLowercaseBrandKey()
  {
    Assert.Equal("controlr.desktop.service", BrandingConstants.LinuxDesktopServiceName);
  }

  [Fact]
  public void LinuxInstallDirectoryName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.LinuxInstallDirectoryName);
  }

  [Fact]
  public void MacAppBundleBaseName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.MacAppBundleBaseName);
  }

  [Fact]
  public void MacBundleStateDirectoryName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.MacBundleStateDirectoryName);
  }

  [Fact]
  public void MacInstallDirectoryName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.MacInstallDirectoryName);
  }

  [Fact]
  public void MacServicePrefix_PrefixedWithApp()
  {
    Assert.Equal("app.controlr", BrandingConstants.MacServicePrefix);
  }

  [Fact]
  public void Publisher_IsBitbound()
  {
    Assert.Equal("Bitbound", BrandingConstants.Publisher);
  }

  [Fact]
  public void RepairStageDirectoryPrefix_PrefixedWithDotLowercase()
  {
    Assert.Equal(".controlr-desktop-repair-", BrandingConstants.RepairStageDirectoryPrefix);
  }

  [Fact]
  public void UnixConfigDirectoryName_IsLowercase()
  {
    Assert.Equal("controlr", BrandingConstants.UnixConfigDirectoryName);
  }

  [Fact]
  public void UnixHiddenDirectoryName_PrefixedWithDot()
  {
    Assert.Equal(".controlr", BrandingConstants.UnixHiddenDirectoryName);
  }

  [Fact]
  public void UnixLogDirectoryName_IsLowercase()
  {
    Assert.Equal("controlr", BrandingConstants.UnixLogDirectoryName);
  }

  [Fact]
  public void UpdaterTempDirectoryName_UsesBrandKeyWithSuffix()
  {
    Assert.Equal("ControlR_Update", BrandingConstants.UpdaterTempDirectoryName);
  }

  [Fact]
  public void WindowsInstallDirectoryName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.WindowsInstallDirectoryName);
  }

  [Fact]
  public void WindowsLogDirectoryName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.WindowsLogDirectoryName);
  }

  [Fact]
  public void WindowsServiceBaseName_UsesBrandKey()
  {
    Assert.Equal("ControlR.Agent", BrandingConstants.WindowsServiceBaseName);
  }

  [Fact]
  public void WindowsUninstallRegistryKeyName_EqualsBrandKey()
  {
    Assert.Equal("ControlR", BrandingConstants.WindowsUninstallRegistryKeyName);
  }
}
