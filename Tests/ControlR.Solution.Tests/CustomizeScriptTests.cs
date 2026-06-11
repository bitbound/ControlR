using System.Diagnostics;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Solution.Tests;

public class CustomizeScriptTests
{
  private readonly ITestOutputHelper _output;
  private readonly string _ps1Path;
  private readonly string _solutionDir;

  public CustomizeScriptTests(ITestOutputHelper output)
  {
    _output = output;
    var solutionDirResult = IoHelper.GetSolutionDir();
    Assert.True(solutionDirResult.IsSuccess, $"Failed to find solution directory: {solutionDirResult.Reason}");
    _solutionDir = solutionDirResult.Value;
    _ps1Path = Path.Combine(_solutionDir, ".scripts", "customize.ps1");
    Assert.True(File.Exists(_ps1Path), $"Script not found at {_ps1Path}. This usually means you're running tests against a repo that doesn't include .scripts/.");
  }

  [Fact]
  public async Task BrandNameStartingWithDigit_Fails()
  {
    var result = await RunScript("-BrandName", "123Brand", "-WhatIf");
    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("BrandName must start with a letter", result.StandardError);
  }

  [Fact]
  public async Task BrandNameWithSpaces_Succeeds()
  {
    var result = await RunScript("-BrandName", "My Space Brand", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    AssertOutputMatchesBrandValues(result, "My Space Brand", "My_Space_Brand");
  }

  [Fact]
  public async Task BrandNameWithSpecialChars_Fails()
  {
    var result = await RunScript("-BrandName", "Brand!@#", "-WhatIf");
    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("BrandName must start with a letter", result.StandardError);
  }

  [Fact]
  public async Task CompanyLogoPng_NotProvided_DefaultMessage()
  {
    var result = await RunScript("-BrandName", "NoLogoTest", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    Assert.Contains("No company logo provided", result.StandardOutput);
  }

  [Fact]
  public async Task CompanyLogoPng_Provided_Succeeds()
  {
    var result = await RunScript("-BrandName", "LogoTest", "-CompanyLogoPng", "./my-logo.png", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    Assert.Contains("Would download/copy company logo from: ./my-logo.png", result.StandardOutput);
  }

  [Fact]
  public async Task DefaultBrandName_NoChangesDetected()
  {
    var result = await RunScript("-WhatIf");
    Assert.Equal(0, result.ExitCode);
    // Default brand = "ControlR" which matches file contents, so no diffs should be reported
    Assert.DoesNotContain("[What-If] Would change", result.StandardOutput);
  }

  [Fact]
  public async Task EmptyColor_Fails()
  {
    var result = await RunScript("-BrandName", "TestBrand", "-PrimaryColorDark", "", "-WhatIf");
    Assert.NotEqual(0, result.ExitCode);
  }

  [Fact]
  public async Task HexColorWithHash_Succeeds()
  {
    var result = await RunScript(
      "-BrandName", "HashTest",
      "-PrimaryColorDark", "#FF1122",
      "-SecondaryColorDark", "#334455",
      "-WhatIf"
    );
    Assert.Equal(0, result.ExitCode);
    AssertContainsLine(result, "PrimaryColorDark = \"FF1122\"");
    AssertContainsLine(result, "SecondaryColorDark = \"334455\"");
  }

  [Theory]
  [InlineData("GGGGGG")]
  [InlineData("ZZZZZZ")]
  [InlineData("123")]
  [InlineData("FFFFFFF")]
  public async Task InvalidHexColor_Fails(string color)
  {
    var result = await RunScript("-BrandName", "TestBrand", "-PrimaryColorDark", color, "-WhatIf");
    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("invalid hex color", result.StandardError.ToLowerInvariant());
  }

  [Fact]
  public async Task ValidBrandName_ControlR_NoChanges()
  {
    var result = await RunScript("-BrandName", "ControlR", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    // BrandName "ControlR" matches current file values, so no diffs
    Assert.DoesNotContain("[What-If] Would change", result.StandardOutput);
  }

  [Fact]
  public async Task ValidBrandName_MyBrand_Succeeds()
  {
    var result = await RunScript("-BrandName", "MyBrand", "-Publisher", "MyPublisher", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    AssertOutputMatchesBrandValues(result, "MyBrand", "MyBrand", "MyPublisher");
  }

  [Fact]
  public async Task ValidBrandName_WithHyphens_Succeeds()
  {
    var result = await RunScript("-BrandName", "My-Brand", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    AssertOutputMatchesBrandValues(result, "My-Brand", "My_Brand");
  }

  [Fact]
  public async Task ValidBrandName_WithPeriods_Succeeds()
  {
    var result = await RunScript("-BrandName", "My.Brand", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    AssertOutputMatchesBrandValues(result, "My.Brand", "My_Brand");
  }

  [Fact]
  public async Task ValidBrandName_WithUnderscores_Succeeds()
  {
    var result = await RunScript("-BrandName", "My_Brand", "-WhatIf");
    Assert.Equal(0, result.ExitCode);
    AssertOutputMatchesBrandValues(result, "My_Brand", "My_Brand");
  }

  [Fact]
  public async Task ValidHexColors_AppearInAXAMLTheme()
  {
    var result = await RunScript(
      "-BrandName", "AxamlTest",
      "-PrimaryColorDark", "FF1122",
      "-SecondaryColorDark", "334455",
      "-TertiaryColorDark", "667788",
      "-InfoColorDark", "99AABB",
      "-SuccessColorDark", "CCDDEE",
      "-WarningColorDark", "112233",
      "-ErrorColorDark", "445566",
      "-PrimaryColorLight", "EEFF00",
      "-SecondaryColorLight", "00EEFF",
      "-TertiaryColorLight", "FF00EE",
      "-InfoColorLight", "AABBCC",
      "-SuccessColorLight", "CCBBAA",
      "-WarningColorLight", "BBAA99",
      "-ErrorColorLight", "998877",
      "-WhatIf"
    );
    Assert.Equal(0, result.ExitCode);

    // Theme.axaml Dark section — 7 primary color fields
    AssertContainsLine(result, "PrimaryColor\" Color=\"#FF1122");
    AssertContainsLine(result, "SecondaryColor\" Color=\"#334455");
    AssertContainsLine(result, "TertiaryColor\" Color=\"#667788");
    AssertContainsLine(result, "InfoColor\" Color=\"#99AABB");
    AssertContainsLine(result, "SuccessColor\" Color=\"#CCDDEE");
    AssertContainsLine(result, "WarningColor\" Color=\"#112233");
    AssertContainsLine(result, "ErrorColor\" Color=\"#445566");

    // Theme.axaml Light section — 7 primary color fields
    AssertContainsLine(result, "PrimaryColor\" Color=\"#EEFF00");
    AssertContainsLine(result, "SecondaryColor\" Color=\"#00EEFF");
    AssertContainsLine(result, "TertiaryColor\" Color=\"#FF00EE");
    AssertContainsLine(result, "InfoColor\" Color=\"#AABBCC");
    AssertContainsLine(result, "SuccessColor\" Color=\"#CCBBAA");
    AssertContainsLine(result, "WarningColor\" Color=\"#BBAA99");
    AssertContainsLine(result, "ErrorColor\" Color=\"#998877");

    // App.axaml — Accent color replaced in both Dark and Light palettes
    AssertContainsLine(result, "Accent=\"#FF1122");
  }

  [Fact]
  public async Task ValidHexColors_AppearInOutput()
  {
    var result = await RunScript(
      "-BrandName", "ColorTest",
      "-PrimaryColorDark", "FF0000",
      "-SecondaryColorDark", "00FF00",
      "-TertiaryColorDark", "0000FF",
      "-InfoColorDark", "AABBCC",
      "-SuccessColorDark", "CCDD00",
      "-WarningColorDark", "FFCC00",
      "-ErrorColorDark", "DD4444",
      "-PrimaryColorLight", "112233",
      "-SecondaryColorLight", "445566",
      "-TertiaryColorLight", "778899",
      "-InfoColorLight", "AABB11",
      "-SuccessColorLight", "223344",
      "-WarningColorLight", "556677",
      "-ErrorColorLight", "8899AA",
      "-WhatIf"
    );
    Assert.Equal(0, result.ExitCode);
    // Verify colors appear in BrandingConstants.cs diff
    AssertContainsLine(result, "PrimaryColorDark = \"FF0000\"");
    AssertContainsLine(result, "SecondaryColorDark = \"00FF00\"");
    AssertContainsLine(result, "TertiaryColorDark = \"0000FF\"");
    AssertContainsLine(result, "InfoColorDark = \"AABBCC\"");
    AssertContainsLine(result, "SuccessColorDark = \"CCDD00\"");
    AssertContainsLine(result, "WarningColorDark = \"FFCC00\"");
    AssertContainsLine(result, "ErrorColorDark = \"DD4444\"");
    AssertContainsLine(result, "PrimaryColorLight = \"112233\"");
    AssertContainsLine(result, "SecondaryColorLight = \"445566\"");
    AssertContainsLine(result, "TertiaryColorLight = \"778899\"");
    AssertContainsLine(result, "InfoColorLight = \"AABB11\"");
    AssertContainsLine(result, "SuccessColorLight = \"223344\"");
    AssertContainsLine(result, "WarningColorLight = \"556677\"");
    AssertContainsLine(result, "ErrorColorLight = \"8899AA\"");
  }

  [Fact]
  public async Task WhatIfMode_DoesNotModifyFiles()
  {
    var filePath = "Directory.Build.props";
    var before = File.GetLastWriteTime(filePath);
    Thread.Sleep(100);

    var result = await RunScript("-BrandName", "TestWhatIf", "-WhatIf");
    Assert.Equal(0, result.ExitCode);

    var after = File.GetLastWriteTime(filePath);
    Assert.Equal(before, after);
  }

  private void AssertContainsLine(ProcessResult result, string expectedLine)
  {
    Assert.Contains(expectedLine, result.StandardOutput);
  }

  private void AssertOutputMatchesBrandValues(ProcessResult result, string brandName, string brandKey, string? publisher = null)
  {
    // Directory.Build.props: BrandPrefix and Substring
    AssertContainsLine(result, $"<BrandPrefix>{brandKey}</BrandPrefix>");

    // BrandingConstants.cs: BrandName
    AssertContainsLine(result, $"BrandName = \"{brandName}\"");

    // When a custom publisher is provided, verify it appears in the diff
    if (publisher is not null)
    {
      AssertContainsLine(result, $"Publisher = \"{publisher}\"");
      AssertContainsLine(result, "Copyright");
      AssertContainsLine(result, $"{publisher}. All rights reserved");
    }

    // Info.plist: BrandName replaces "ControlR" strings
    AssertContainsLine(result, $"<string>{brandName}</string>");
    AssertContainsLine(result, $"{brandName} uses notifications");

    // appsettings.json: AuthenticatorIssuerName
    AssertContainsLine(result, $"AuthenticatorIssuerName\": \"{brandName}\"");

    // Docker-compose
    AssertContainsLine(result, brandKey.ToLowerInvariant());

    // manifest.webmanifest
    AssertContainsLine(result, $"\"name\": \"{brandName}\"");
    AssertContainsLine(result, $"\"short_name\": \"{brandName}\"");

    // Localization strings
    AssertContainsLine(result, $"{brandName} Chat");
    AssertContainsLine(result, $"{brandName} is free, open-source");
    AssertContainsLine(result, $"{brandName} uses the following");

    // Service templates
    AssertContainsLine(result, $"{brandName} is an open-source");
    AssertContainsLine(result, $"{brandName} Desktop Client provides");

    // OpenAPI
    AssertContainsLine(result, $"{brandKey}.Web.Server");

    // Installer Program.cs
    AssertContainsLine(result, $"{brandName} agent installer");
    AssertContainsLine(result, $"{brandName} agent bundle");
  }

  private async Task<ProcessResult> RunScript(params string[] args)
  {
    var ps1Quoted = $"\"{_ps1Path}\"";
    var quotedArgs = args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
    var arguments = $"-NonInteractive -NoProfile -File {ps1Quoted} {string.Join(" ", quotedArgs)}";

    var startInfo = new ProcessStartInfo
    {
      FileName = "pwsh",
      Arguments = arguments,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      WorkingDirectory = _solutionDir,
    };

    var outputLines = new List<string>();
    var errorLines = new List<string>();

    using var process = Process.Start(startInfo);
  
    Assert.NotNull(process);

    process.OutputDataReceived += (sender, e) =>
    {
      if (e.Data is not null)
        outputLines.Add(e.Data);
    };
    process.ErrorDataReceived += (sender, e) =>
    {
      if (e.Data is not null)
        errorLines.Add(e.Data);
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    try
    {
      await process.WaitForExitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
      process.Kill(true);
      process.WaitForExitAsync().Wait();
      throw new TimeoutException(
        $"Process '{startInfo.FileName} {arguments}' did not exit within 60s.");
    }

    var stdout = string.Join("\n", outputLines);
    var stderr = string.Join("\n", errorLines);

    _output.WriteLine(stdout);

    if (!string.IsNullOrWhiteSpace(stderr))
      _output.WriteLine($"STDERR: {stderr}");
      
    return new ProcessResult(process.ExitCode, stdout, stderr);
  }
}

internal record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
