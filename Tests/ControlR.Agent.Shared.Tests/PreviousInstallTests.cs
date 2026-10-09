using ControlR.Agent.Shared.Models;

namespace ControlR.Agent.Shared.Tests;

public class PreviousInstallTests
{
  [Fact]
  public void Resolve_WhenBrandDiffers_CarriesItsBrandAndLeavesTheInstanceIdDefault()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousBrandName: "Acme Remote",
      previousInstanceId: null,
      installerBrandName: "ControlR",
      installerInstanceId: "exp");

    Assert.NotNull(previousInstall);
    Assert.Equal("Acme Remote", previousInstall.BrandName);
    // The agent omits --previous-instance-id when the replaced install used the default instance id.
    Assert.Null(previousInstall.InstanceId);
  }

  [Fact]
  public void Resolve_WhenOnlyTheInstanceIdDiffers_UsesThisInstallersBrand()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousBrandName: null,
      previousInstanceId: "exp",
      installerBrandName: "ControlR",
      installerInstanceId: "ce");

    Assert.NotNull(previousInstall);
    Assert.Equal("ControlR", previousInstall.BrandName);
    Assert.Equal("exp", previousInstall.InstanceId);
  }

  [Fact]
  public void Resolve_WhenTheArgumentsDescribeThisSameInstall_ReturnsNull()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousBrandName: "ControlR",
      previousInstanceId: "exp",
      installerBrandName: "ControlR",
      installerInstanceId: "exp");

    // Replacing this install would remove what this install is in the middle of writing.
    Assert.Null(previousInstall);
  }

  [Fact]
  public void Resolve_WhenTheBrandDiffersOnlyByPunctuation_ReturnsNull()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousBrandName: "Acme-Remote",
      previousInstanceId: null,
      installerBrandName: "Acme Remote",
      installerInstanceId: null);

    // Both names reduce to the same key, so they name one install, not two.
    Assert.Null(previousInstall);
  }

  [Fact]
  public void Resolve_WhenTheReplacedInstallUsedTheDefaultInstanceId_KeepsItRatherThanThisInstallers()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousBrandName: "Acme Remote",
      previousInstanceId: null,
      installerBrandName: "ControlR",
      installerInstanceId: "ce");

    // Defaulting the instance id to this installer's would send the stop and the retire at this
    // installer's own directory instead of the one being replaced.
    Assert.NotNull(previousInstall);
    Assert.Equal("Acme Remote", previousInstall.BrandName);
    Assert.Null(previousInstall.InstanceId);
  }

  [Fact]
  public void Resolve_WithNoArguments_ReturnsNull()
  {
    Assert.Null(PreviousInstall.Resolve(
      previousBrandName: null,
      previousInstanceId: null,
      installerBrandName: "ControlR",
      installerInstanceId: "exp"));

    Assert.Null(PreviousInstall.Resolve(
      previousBrandName: "  ",
      previousInstanceId: "",
      installerBrandName: "ControlR",
      installerInstanceId: "exp"));
  }
}
