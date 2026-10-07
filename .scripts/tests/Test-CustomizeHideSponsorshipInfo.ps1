# Functional tests for the hideSponsorshipInfo flag in customize.ps1 / Invoke-Customize.ps1.
# Builds a disposable mini repo (the scripts locate the repo root via $PSScriptRoot/..)
# and exercises the HideSponsorshipInfo paths. Run: pwsh -File Test-CustomizeHideSponsorshipInfo.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$falseDeclaration = "public static bool HideSponsorshipInfo { get; } = false;"
$trueDeclaration = "public static bool HideSponsorshipInfo { get; } = true;"
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
  param([string] $Name, [switch] $OmitDeclaration)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-sponsorship-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  $declaration = if ($OmitDeclaration) { "" } else { "  public static bool HideSponsorshipInfo { get; } = false;`n" }
  $brandingContent = @"
namespace ControlR.Libraries.Branding;

public static class BrandingConstants
{
  public const string BrandName = "ControlR";
  public const string Publisher = "Bitbound";
$declaration}
"@
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value $brandingContent -Encoding UTF8

  return $root
}

function Get-BrandingConstantsContent {
  param([string] $Root)
  return Get-Content -LiteralPath (Join-Path $Root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Raw -Encoding UTF8
}

function New-ConfigPayload {
  param([string] $Root, [bool] $HideSponsorshipInfo = $false, [switch] $OmitHideSponsorshipInfo)
  $payload = [ordered]@{ brandName = "Acme"; publisher = "Acme Inc."; version = "1.2.3.4"; colors = $null; images = $null }
  if (-not $OmitHideSponsorshipInfo) {
    $payload.hideSponsorshipInfo = $HideSponsorshipInfo
  }
  $path = Join-Path $Root "config.json"
  Set-Content -Path $path -Value ($payload | ConvertTo-Json) -Encoding UTF8
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

# customize.ps1 without the switch leaves the constant false.
$repo = New-FakeRepo -Name "absent"
Invoke-CustomizeScript -Root $repo
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains($falseDeclaration)) -TestName "customize.ps1 without -HideSponsorshipInfo leaves the constant false"

# customize.ps1 with the switch sets the constant true.
$repo = New-FakeRepo -Name "true"
Invoke-CustomizeScript -Root $repo -ExtraParams @{ HideSponsorshipInfo = $true }
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains($trueDeclaration)) -TestName "customize.ps1 -HideSponsorshipInfo sets the constant true"

# Invoke-Customize.ps1 forwards true from the customization payload.
$repo = New-FakeRepo -Name "payloadtrue"
$configPath = New-ConfigPayload -Root $repo -HideSponsorshipInfo $true
Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains($trueDeclaration)) -TestName "Invoke-Customize.ps1 sets the constant true from the customization payload"

# A payload with the flag explicitly false leaves the constant false.
$repo = New-FakeRepo -Name "payloadfalse"
$configPath = New-ConfigPayload -Root $repo -HideSponsorshipInfo $false
Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains($falseDeclaration)) -TestName "Invoke-Customize.ps1 with the flag false leaves the constant false"

# A payload without the flag (produced before it existed) must not trip Set-StrictMode and
# leaves the constant false.
$repo = New-FakeRepo -Name "legacy"
$configPath = New-ConfigPayload -Root $repo -OmitHideSponsorshipInfo
Invoke-CustomizeFromConfig -Root $repo -ConfigPath $configPath
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains($falseDeclaration)) -TestName "Invoke-Customize.ps1 without the payload flag leaves the constant false"

# customize.ps1 fails loudly when -HideSponsorshipInfo is requested but the declaration is
# missing, so a reformatted source cannot silently ship the sponsorship links.
$repo = New-FakeRepo -Name "nodeclaration" -OmitDeclaration
Assert-True -Condition (Test-Throws { Invoke-CustomizeScript -Root $repo -ExtraParams @{ HideSponsorshipInfo = $true } }) -TestName "customize.ps1 -HideSponsorshipInfo fails when the declaration is missing"

# Without the switch a missing declaration is not an error, because nothing was asked of it.
$repo = New-FakeRepo -Name "nodeclarationdefault" -OmitDeclaration
Invoke-CustomizeScript -Root $repo
Assert-True -Condition ((Get-BrandingConstantsContent -Root $repo).Contains("public const string BrandName")) -TestName "customize.ps1 without -HideSponsorshipInfo tolerates a missing declaration"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All hideSponsorshipInfo script tests passed." -ForegroundColor Green
