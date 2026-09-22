# Functional tests for custom CSS handling in customize.ps1 / Invoke-Customize.ps1.
# Builds a disposable mini repo (the scripts locate the repo root via $PSScriptRoot/..)
# and exercises the custom.css replacement paths. Run: pwsh -File Test-CustomizeCustomCss.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$placeholder = "/* Placeholder for custom CSS */"
$sponsorCss = "body { background: #101820; }`n.nav { display: none; }"
$failures = @()

function Assert-Equal {
  param([string] $Expected, [string] $Actual, [string] $TestName)
  if ($Expected -ne $Actual) {
    $script:failures += "$TestName`n  expected: $Expected`n  actual:   $Actual"
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

function New-FakeRepo {
  param([string] $Name)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-css-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value 'namespace ControlR.Libraries.Branding;' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static") -Force | Out-Null
  Set-Content -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static/custom.css") -Value $placeholder -Encoding UTF8

  return $root
}

function Get-CustomCssContent {
  param([string] $Root)
  return (Get-Content -LiteralPath (Join-Path $Root "ControlR.Web.Server/wwwroot/static/custom.css") -Raw -Encoding UTF8).Trim()
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

$repo = New-FakeRepo -Name "replace"
Invoke-CustomizeScript -Root $repo -ExtraParams @{ CustomCss = $sponsorCss }
Assert-Equal -Expected $sponsorCss -Actual (Get-CustomCssContent -Root $repo) -TestName "customize.ps1 -CustomCss replaces the placeholder entirely"

$repo = New-FakeRepo -Name "keep"
Invoke-CustomizeScript -Root $repo
Assert-Equal -Expected $placeholder -Actual (Get-CustomCssContent -Root $repo) -TestName "customize.ps1 without -CustomCss keeps the placeholder"

$repo = New-FakeRepo -Name "whatif"
Invoke-CustomizeScript -Root $repo -ExtraParams @{ CustomCss = $sponsorCss; WhatIf = $true }
Assert-Equal -Expected $placeholder -Actual (Get-CustomCssContent -Root $repo) -TestName "customize.ps1 -WhatIf does not modify custom.css"

$repo = New-FakeRepo -Name "payload"
$payload = [ordered]@{ brandName = "Acme"; publisher = "Acme Inc."; version = "1.2.3.4"; colors = $null; images = $null; customCss = $sponsorCss } | ConvertTo-Json
Set-Content -Path (Join-Path $repo "config.json") -Value $payload -Encoding UTF8
$prevEnvFile = $env:GITHUB_ENV
try {
  $env:GITHUB_ENV = Join-Path $repo "github-env.txt"
  & (Join-Path $repo ".scripts/Invoke-Customize.ps1") -ConfigPath (Join-Path $repo "config.json") -Version "1.2.3.4"
}
finally {
  $env:GITHUB_ENV = $prevEnvFile
}
Assert-Equal -Expected $sponsorCss -Actual (Get-CustomCssContent -Root $repo) -TestName "Invoke-Customize.ps1 passes customCss from the payload"

# Payload without a customCss property at all (portal deployed before the feature)
# must not trip Set-StrictMode and must keep the placeholder.
$repo = New-FakeRepo -Name "legacy"
$payload = [ordered]@{ brandName = "Acme"; publisher = "Acme Inc."; version = "1.2.3.4"; colors = $null; images = $null } | ConvertTo-Json
Set-Content -Path (Join-Path $repo "config.json") -Value $payload -Encoding UTF8
$prevEnvFile = $env:GITHUB_ENV
try {
  $env:GITHUB_ENV = Join-Path $repo "github-env.txt"
  & (Join-Path $repo ".scripts/Invoke-Customize.ps1") -ConfigPath (Join-Path $repo "config.json") -Version "1.2.3.4"
}
finally {
  $env:GITHUB_ENV = $prevEnvFile
}
Assert-Equal -Expected $placeholder -Actual (Get-CustomCssContent -Root $repo) -TestName "Invoke-Customize.ps1 tolerates payloads without customCss"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All custom CSS script tests passed." -ForegroundColor Green
