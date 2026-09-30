# Functional tests for the Xcode band selection in Select-MacOSXcode.ps1.
# Builds fake Xcode bundles (real directories plus relative and absolute symlinks) and
# stub xcodebuild executables, so the resolution logic runs without Xcode present.
# Run: pwsh -File Test-Select-MacOSXcode.ps1
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

# A stub stand-in for /Applications/Xcode*.app/Contents/Developer/usr/bin/xcodebuild.
# It ignores its arguments and reports the version baked into the bundle, which is what
# the selector parses.
function Add-FakeBundle {
  param([string] $ApplicationsPath, [string] $BundleName, [string] $Version)
  $bundle = Join-Path $ApplicationsPath $BundleName
  $bin = Join-Path $bundle "Contents/Developer/usr/bin"
  New-Item -ItemType Directory -Path $bin -Force | Out-Null
  $stub = Join-Path $bin "xcodebuild"
  # Written with LF endings regardless of how this file checked out. A CR before the
  # newline after the shebang stops the kernel from recognizing an interpreter.
  $content = @(
    '#!/bin/sh'
    "echo `"Xcode $Version`""
    'echo "Build version 17A400"'
  ) -join "`n"
  [System.IO.File]::WriteAllText($stub, $content + "`n")
  # A native symlink needs no execute bit under pwsh on Windows, but the same test runs on
  # Linux CI where the selector invokes the stub directly.
  if (-not $IsWindows) { & chmod +x $stub }
  return $bundle
}

function New-FakeApplications {
  param([string] $Name)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("xcode-select-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
  $apps = Join-Path $root "/Applications"
  New-Item -ItemType Directory -Path $apps -Force | Out-Null
  return $apps
}

function New-PropsFile {
  param([string] $ApplicationsPath, [string] $Band)
  $props = Join-Path (Split-Path -Parent $ApplicationsPath) "Directory.Build.props"
  Set-Content -Path $props -Value "<Project><PropertyGroup><MacTargetPlatformVersion>$Band</MacTargetPlatformVersion></PropertyGroup></Project>" -Encoding UTF8
  return $props
}

# Runs the selector against the fake tree and returns the DEVELOPER_DIR it wrote. If
# $env:GITHUB_ENV is unset the selector only sets the process variable, so it is forced to
# a temp file here to observe what a CI step would receive.
function Get-Selection {
  param([string] $ApplicationsPath, [string] $PropsPath)
  $envFile = Join-Path (Split-Path -Parent $ApplicationsPath) "github-env.txt"
  if (Test-Path -LiteralPath $envFile) { Remove-Item -LiteralPath $envFile -Force }

  $prevEnvFile = $env:GITHUB_ENV
  $prevDeveloperDir = $env:DEVELOPER_DIR
  try {
    $env:GITHUB_ENV = $envFile
    $env:DEVELOPER_DIR = $null
    & (Join-Path $PSScriptRoot "../Select-MacOSXcode.ps1") -PropsPath $PropsPath -ApplicationsPath $ApplicationsPath *>&1 | Out-Null
  }
  finally {
    $env:GITHUB_ENV = $prevEnvFile
    $env:DEVELOPER_DIR = $prevDeveloperDir
  }

  $line = Select-String -Path $envFile -Pattern '^DEVELOPER_DIR=(.+)$' | Select-Object -Last 1
  if (-not $line) {
    throw "The selector did not write DEVELOPER_DIR to GITHUB_ENV."
  }
  # A trailing carriage return would mean the step fed a broken path to later steps.
  $value = $line.Matches[0].Groups[1].Value
  if ($value -ne $value.Trim()) {
    throw "DEVELOPER_DIR contains stray whitespace (a CR would break later steps): [$value]"
  }
  return $value
}

function New-Symlink {
  param([string] $LinkPath, [string] $Target)
  # Needed on Windows; a no-op on Linux and macOS.
  New-Item -ItemType SymbolicLink -Path $LinkPath -Target $Target -ErrorAction Stop | Out-Null
}

$scriptPath = Join-Path $PSScriptRoot "../Select-MacOSXcode.ps1"

# --- Relative symlink, the shape the review flagged -------------------------------------
# Xcode_26.0.app -> Xcode_26.0.1.app, both in the same directory. Resolving the target
# against the process working directory instead of the link's parent would miss it and
# leave DEVELOPER_DIR pointing through the symlink.
$apps = New-FakeApplications -Name "relative"
$real = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.1.app" -Version "26.0.1"
New-Symlink -LinkPath (Join-Path $apps "Xcode_26.0.app") -Target "Xcode_26.0.1.app"
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $real "Contents/Developer") -Actual $resolved -TestName "resolves a relative Xcode symlink to the real bundle"

# --- Absolute symlink ----------------------------------------------------------------------
$apps = New-FakeApplications -Name "absolute"
$real = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.1.app" -Version "26.0.1"
New-Symlink -LinkPath (Join-Path $apps "Xcode_26.0.app") -Target $real
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $real "Contents/Developer") -Actual $resolved -TestName "resolves an absolute Xcode symlink to the real bundle"

# --- Real bundle needs no resolution ------------------------------------------------------
$apps = New-FakeApplications -Name "real"
$real = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode-26.0.1.app" -Version "26.0.1"
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $real "Contents/Developer") -Actual $resolved -TestName "selects an xcodes-named real bundle by reported version"

# --- Highest patch inside the band wins ---------------------------------------------------
$apps = New-FakeApplications -Name "highest"
Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.0.app" -Version "26.0.0" | Out-Null
$best = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.2.app" -Version "26.0.2"
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $best "Contents/Developer") -Actual $resolved -TestName "picks the highest patch inside the pinned band"

# --- Out-of-band versions are ignored -----------------------------------------------------
$apps = New-FakeApplications -Name "band"
$inBand = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.1.app" -Version "26.0.1"
Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.6.app" -Version "26.6" | Out-Null
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $inBand "Contents/Developer") -Actual $resolved -TestName "excludes an Xcode outside the pinned band"

# --- Two symlinks onto one bundle are deduplicated ----------------------------------------
$apps = New-FakeApplications -Name "dedupe"
$real = Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.1.app" -Version "26.0.1"
New-Symlink -LinkPath (Join-Path $apps "Xcode_26.0.app") -Target "Xcode_26.0.1.app"
New-Symlink -LinkPath (Join-Path $apps "Xcode.app") -Target "Xcode_26.0.1.app"
$resolved = Get-Selection -ApplicationsPath $apps -PropsPath (New-PropsFile -ApplicationsPath $apps -Band "26.0")
Assert-Equal -Expected (Join-Path $real "Contents/Developer") -Actual $resolved -TestName "collapses several symlinks onto one real bundle"

# --- No match fails closed rather than guessing -------------------------------------------
$apps = New-FakeApplications -Name "nomatch"
Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.6.app" -Version "26.6" | Out-Null
$props = New-PropsFile -ApplicationsPath $apps -Band "26.0"
Assert-Throws -Action { Get-Selection -ApplicationsPath $apps -PropsPath $props } -TestName "fails when no installed Xcode matches the pinned band"

# --- A missing band is a configuration error -----------------------------------------------
$apps = New-FakeApplications -Name "noband"
Add-FakeBundle -ApplicationsPath $apps -BundleName "Xcode_26.0.1.app" -Version "26.0.1" | Out-Null
$props = Join-Path (Split-Path -Parent $apps) "Directory.Build.props"
Set-Content -Path $props -Value "<Project><PropertyGroup></PropertyGroup></Project>" -Encoding UTF8
Assert-Throws -Action { Get-Selection -ApplicationsPath $apps -PropsPath $props } -TestName "fails when MacTargetPlatformVersion is absent"

# --- A link cycle terminates instead of hanging ---------------------------------------------
$apps = New-FakeApplications -Name "cycle"
New-Symlink -LinkPath (Join-Path $apps "Xcode_26.0.app") -Target "Xcode_26.1.app"
New-Symlink -LinkPath (Join-Path $apps "Xcode_26.1.app") -Target "Xcode_26.0.app"
$props = New-PropsFile -ApplicationsPath $apps -Band "26.0"
Assert-Throws -Action { Get-Selection -ApplicationsPath $apps -PropsPath $props } -TestName "terminates on a symlink cycle instead of spinning"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All Xcode selection script tests passed." -ForegroundColor Green
