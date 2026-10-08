namespace ControlR.Libraries.Branding.Tests;

public class BrandNamesTests
{
  [Fact]
  public void AgentBaseName_ForAForeignBrand_UsesThatBrandsKey()
  {
    // The executable of an install is named for its own brand, which is not this build's.
    Assert.Equal("Acme_Remote.Agent", new BrandNames("Acme Remote").AgentBaseName);
  }

  [Fact]
  public void BrandKey_ForAPaddedBrandName_KeepsThePaddingLikeTheBuildScript()
  {
    // customize.ps1 derives the key with the same untrimmed replace, so trimming here would send this
    // build looking for files named differently than the ones the script shipped.
    Assert.Equal("Acme_", new BrandNames("Acme ").BrandKey);
  }

  [Fact]
  public void Current_MatchesTheCompiledInBrand()
  {
    Assert.Equal(BrandingConstants.AgentBaseName, BrandNames.Current.AgentBaseName);
    Assert.Equal(BrandingConstants.BundleHashFileName, BrandNames.Current.BundleHashFileName);
    Assert.Equal(BrandingConstants.IpcPipeBaseName, BrandNames.Current.IpcPipeBaseName);
    Assert.Equal(BrandingConstants.WindowsServiceBaseName, BrandNames.Current.WindowsServiceBaseName);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void MissingBrandName_Throws(string? brandName)
  {
    Assert.Throws<ArgumentException>(() => new BrandNames(brandName!));
  }

  [Fact]
  public void TwoNamesSharingAKey_ProduceTheSameNames()
  {
    Assert.Equal(new BrandNames("Foo_Bar").AgentBaseName, new BrandNames("Foo-Bar").AgentBaseName);
  }
}
