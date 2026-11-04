using ControlR.Agent.Shared.Models;

namespace ControlR.Agent.Shared.Tests;

public class PreviousInstallTests
{
  [Fact]
  public void Resolve_WhenBrandDiffers_CarriesItsBrandAndLeavesTheInstanceIdDefault()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Acme Corp",
      previousBrandName: "Acme Remote",
      previousInstanceId: null,
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "exp");

    Assert.NotNull(previousInstall);
    Assert.Equal("Acme Remote", previousInstall.BrandName);
    Assert.Equal("Acme Corp", previousInstall.Publisher);
    // The agent omits --previous-instance-id when the replaced install used the default instance id.
    Assert.Null(previousInstall.InstanceId);
  }

  [Fact]
  public void Resolve_WhenOnlyTheInstanceIdDiffers_UsesThisInstallersBrand()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: null,
      previousBrandName: null,
      previousInstanceId: "exp",
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "ce");

    Assert.NotNull(previousInstall);
    Assert.Equal("ControlR", previousInstall.BrandName);
    Assert.Equal("exp", previousInstall.InstanceId);

    // An agent that predates the publisher directory sends no publisher at all. Reading that as this
    // installer's publisher would look for the install being replaced under the vendor directory, where
    // it was never installed.
    Assert.Null(previousInstall.Publisher);
  }

  [Fact]
  public void Resolve_WhenOnlyThePublisherDiffers_CarriesThatPublisher()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Acme Corp",
      previousBrandName: "ControlR",
      previousInstanceId: null,
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: null);

    // The publisher is a directory in the replaced install's paths, so a publisher change names a
    // different install even when the brand and instance id are the same.
    Assert.NotNull(previousInstall);
    Assert.Equal("Acme Corp", previousInstall.Publisher);
  }

  [Fact]
  public void Resolve_WhenTheArgumentsDescribeThisSameInstall_ReturnsNull()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Bitbound",
      previousBrandName: "ControlR",
      previousInstanceId: "exp",
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "exp");

    // Replacing this install would remove what this install is in the middle of writing.
    Assert.Null(previousInstall);
  }

  [Fact]
  public void Resolve_WhenTheBrandDiffersOnlyByPunctuation_ReturnsNull()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Acme Corp",
      previousBrandName: "Acme-Remote",
      previousInstanceId: null,
      installerPublisher: "Acme Corp",
      installerBrandName: "Acme Remote",
      installerInstanceId: null);

    // Both names reduce to the same key, so they name one install, not two.
    Assert.Null(previousInstall);
  }

  [Fact]
  public void Resolve_WhenThePublisherDiffersOnlyByPunctuation_ReturnsNull()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Acme_Corp",
      previousBrandName: "ControlR",
      previousInstanceId: null,
      installerPublisher: "Acme Corp",
      installerBrandName: "ControlR",
      installerInstanceId: null);

    // The sanitized key is what names the vendor directory, so both spellings address one install. Naming
    // a migration here would retire the install this installer just wrote.
    Assert.Null(previousInstall);
  }

  [Fact]
  public void Resolve_WhenTheReplacedInstallIsUnderTheSamePublisher_NamesThatPublisher()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Bitbound",
      previousBrandName: "ControlR",
      previousInstanceId: "exp",
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "ce");

    Assert.NotNull(previousInstall);
    Assert.Equal("Bitbound", previousInstall.Publisher);
    Assert.Equal("exp", previousInstall.InstanceId);
  }

  [Fact]
  public void Resolve_WhenTheReplacedInstallUsedTheDefaultInstanceId_KeepsItRatherThanThisInstallers()
  {
    var previousInstall = PreviousInstall.Resolve(
      previousPublisher: "Acme Corp",
      previousBrandName: "Acme Remote",
      previousInstanceId: null,
      installerPublisher: "Bitbound",
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
      previousPublisher: null,
      previousBrandName: null,
      previousInstanceId: null,
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "exp"));

    Assert.Null(PreviousInstall.Resolve(
      previousPublisher: "",
      previousBrandName: "  ",
      previousInstanceId: "",
      installerPublisher: "Bitbound",
      installerBrandName: "ControlR",
      installerInstanceId: "exp"));
  }
}
