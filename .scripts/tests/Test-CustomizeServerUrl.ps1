# Functional tests for the baked-in ControlR server URL in customize.ps1 / Invoke-Customize.ps1.
# Builds a disposable mini repo (the scripts locate the repo root via $PSScriptRoot/..)
# and exercises the ControlrServerUrl paths. Run: pwsh -File Test-CustomizeServerUrl.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$serverUrl = "https://controlr.example.com"
$normalizedUrl = "https://controlr.example.com/"
$failures = @()

function Assert-True {
  param([bool] $Condition, [string] $TestName)
  if (-not $Condition) {
    $script:failures += $TestName
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

function New-FakeRepo {
  param([string] $Name)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-url-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  $brandingContent = @'
namespace ControlR.Libraries.Branding;

public static class BrandingConstants
{
  public const string BrandName = "ControlR";
  public const string Publisher = "Bitbound";

  public static Uri? ControlrServerUrl { get; } = ParseControlrServerUrl(null);

  private static Uri? ParseControlrServerUrl(string? value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
  }
}
'@
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value $brandingContent -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static") -Force | Out-Null
  Set-Content -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static/custom.css") -Value "/* placeholder */" -Encoding UTF8

  return $root
}

function Get-BrandingConstantsContent {
  param([string] $Root)
  return Get-Content -LiteralPath (Join-Path $Root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Raw -Encoding UTF8
}

function New-ConfigPayload {
  param([string] $Root)
  $payload = [ordered]@{ brandName = "Acme"; publisher = "Acme Inc."; version = "1.2.3.4"; colors = $null; images = $null } | ConvertTo-Json
  $path = Join-Path $Root "config.json"
  Set-Content -Path $path -Value $payload -Encoding UTF8
  return $path
}

function Invoke-CustomizeScript {
  param([string] $Root, [hashtable] $ExtraParams = @{})
  $prevEnvFile = $env:GITHUB_ENV
  try {
    $env:GITHUB_ENV = Join-Path $Root "github-env.txt"
    & (Join-Path $Root ".scripts/customize.ps1") -SkipBuild -Version "1.2.3.4" @ExtraParams
  }
  finally {
    $env:GITHUB_ENV = $prevEnvFile
  }
}

function Invoke-CustomizeFromConfig {
  param([string] $Root, [string] $ConfigPath)
  $prevEnvFile = $env:GITHUB_ENV
  try {
    $env:GITHUB_ENV = Join-Path $Root "github-env.txt"
    & (Join-Path $Root ".scripts/Invoke-Customize.ps1") -ConfigPath $ConfigPath -Version "1.2.3.4"
  }
  finally {
    $env:GITHUB_ENV = $prevEnvFile
  }
}

function Test-Throws {
  param([scriptblock] $Action)
  try {
    & $Action
    return $false
  }
  catch {
    return $true
  }
}

# customize.ps1 bakes the URL into the declaration.
$repo = New-FakeRepo -Name "bake"
Invoke-CustomizeScript -Root $repo -ExtraParams @{ ControlrServerUrl = $serverUrl }
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains("ParseControlrServerUrl(`"$normalizedUrl`")")) -TestName "customize.ps1 bakes the ControlR server URL"

# customize.ps1 without a URL leaves the declaration untouched.
$repo = New-FakeRepo -Name "absent"
Invoke-CustomizeScript -Root $repo
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains("ParseControlrServerUrl(null)")) -TestName "customize.ps1 without -ControlrServerUrl leaves the declaration alone"

# customize.ps1 rejects a malformed URL.
$repo = New-FakeRepo -Name "invalid"
Assert-True -Condition (Test-Throws { Invoke-CustomizeScript -Root $repo -ExtraParams @{ ControlrServerUrl = "not a url" } }) -TestName "customize.ps1 rejects a malformed URL"

# customize.ps1 rejects a non-http scheme.
$repo = New-FakeRepo -Name "scheme"
Assert-True -Condition (Test-Throws { Invoke-CustomizeScript -Root $repo -ExtraParams @{ ControlrServerUrl = "ftp://example.com" } }) -TestName "customize.ps1 rejects a non-http scheme"

# Invoke-Customize.ps1 picks the URL up from the environment variable, which is how the
# workflow transports it to a script checked out from an older source ref.
$repo = New-FakeRepo -Name "envvar"
$configPath = New-ConfigPayload -Root $repo
$prevUrl = $env:CONTROLR_SERVER_URL
try {
  $env:CONTROLR_SERVER_URL = $serverUrl
  Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
}
finally {
  $env:CONTROLR_SERVER_URL = $prevUrl
}
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains("ParseControlrServerUrl(`"$normalizedUrl`")")) -TestName "Invoke-Customize.ps1 bakes the URL from CONTROLR_SERVER_URL"

# Invoke-Customize.ps1 without the environment variable leaves the declaration untouched.
$repo = New-FakeRepo -Name "noenvvar"
$configPath = New-ConfigPayload -Root $repo
$prevUrl = $env:CONTROLR_SERVER_URL
try {
  $env:CONTROLR_SERVER_URL = $null
  Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
}
finally {
  $env:CONTROLR_SERVER_URL = $prevUrl
}
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains("ParseControlrServerUrl(null)")) -TestName "Invoke-Customize.ps1 without CONTROLR_SERVER_URL leaves the declaration alone"

# Invoke-Customize.ps1 rejects a malformed URL from the environment variable.
$repo = New-FakeRepo -Name "badenvvar"
$configPath = New-ConfigPayload -Root $repo
$prevUrl = $env:CONTROLR_SERVER_URL
$threw = $false
try {
  $env:CONTROLR_SERVER_URL = "not a url"
  Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
}
catch {
  $threw = $true
}
finally {
  $env:CONTROLR_SERVER_URL = $prevUrl
}
Assert-True -Condition $threw -TestName "Invoke-Customize.ps1 rejects a malformed CONTROLR_SERVER_URL"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All server URL script tests passed." -ForegroundColor Green
