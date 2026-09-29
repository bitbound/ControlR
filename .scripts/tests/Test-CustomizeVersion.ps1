# Functional tests for the version resolution in customize.ps1.
# Covers the '-Version' path and the 'git describe' fallback, including a release tag that
# carries a SemVer prerelease suffix (e.g. 'v0.28.3.0-dev'). Builds a disposable git repo so
# the fallback can resolve a real tag. Run: pwsh -File Test-CustomizeVersion.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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

function Assert-Throws {
  param([scriptblock] $Action, [string] $TestName)
  try {
    & $Action
    $script:failures += "$TestName (expected a throw, but it succeeded)"
  }
  catch {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

# customize.ps1 locates the repo root via $PSScriptRoot/.., so the fake repo needs a .scripts
# copy of the real scripts plus the branding files the script edits before it reaches the
# SkipBuild exit. Git is initialised so 'describe --tags' can resolve a tag.
function New-FakeRepo {
  param([string] $Name, [string] $Tag)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-version-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value 'namespace ControlR.Libraries.Branding;' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static") -Force | Out-Null
  Set-Content -Path (Join-Path $root "ControlR.Web.Server/wwwroot/static/custom.css") -Value "/* placeholder */" -Encoding UTF8

  git -C $root init --quiet | Out-Null
  git -C $root -c user.email=test@example.com -c user.name=test -c commit.gpgsign=false add --all | Out-Null
  git -C $root -c user.email=test@example.com -c user.name=test -c commit.gpgsign=false commit --quiet --no-gpg-sign -m "init" | Out-Null
  # Force a lightweight, unsigned tag so a global tag.gpgSign/forceSignAnnotated setting cannot
  # turn this into an interactive annotated-tag prompt.
  git -C $root -c tag.gpgSign=false -c tag.forceSignAnnotated=false tag $Tag

  return $root
}

# customize.ps1 prints the resolved version to the information stream (Write-Host).
function Get-ResolvedVersion {
  param([string] $Root)
  $prevEnvFile = $env:GITHUB_ENV
  try {
    $env:GITHUB_ENV = Join-Path $Root "github-env.txt"
    $output = & (Join-Path $Root ".scripts/customize.ps1") -SkipBuild 6>&1 | Out-String
  }
  finally {
    $env:GITHUB_ENV = $prevEnvFile
  }

  $match = [regex]::Match($output, 'Revision \+ 1: ([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)')
  if (-not $match.Success) {
    throw "customize.ps1 did not report a resolved version. Output:`n$output"
  }
  return $match.Groups[1].Value
}

# A plain four-part tag: revision 0 increments to 1, build is left alone.
$repo = New-FakeRepo -Name "plain" -Tag "v0.28.3.0"
Assert-Equal -Expected "0.28.3.1" -Actual (Get-ResolvedVersion -Root $repo) -TestName "customize.ps1 increments the revision of a plain latest tag"

# A prerelease-suffixed tag: the '-dev' suffix is dropped before parsing, so the build does not
# fail and the numeric core drives the revision bump.
$repo = New-FakeRepo -Name "dev" -Tag "v0.28.3.0-dev"
Assert-Equal -Expected "0.28.3.1" -Actual (Get-ResolvedVersion -Root $repo) -TestName "customize.ps1 strips a prerelease suffix before bumping the revision"

# A two-segment tag has no build component, so the revision cannot be derived.
$repo = New-FakeRepo -Name "twoseg" -Tag "v2.0"
Assert-Throws -Action { Get-ResolvedVersion -Root $repo } -TestName "customize.ps1 rejects a latest tag with no build component"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All version resolution script tests passed." -ForegroundColor Green
