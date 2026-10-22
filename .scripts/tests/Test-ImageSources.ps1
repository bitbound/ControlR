# Functional tests for image format detection and staging in ImageSources.ps1, plus the
# mislabeled-source rejections in customize.ps1. Asset downloads arrive as extension-free signed URLs,
# so a source's format has to come from its bytes rather than its name.
# Run: pwsh -File Test-ImageSources.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "../ImageSources.ps1")

$failures = @()

function Assert-Equal {
  param($Expected, $Actual, [string] $TestName)
  if ($Expected -ne $Actual) {
    $script:failures += "$TestName`n  expected: $Expected`n  actual:   $Actual"
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

function Assert-Throws {
  param([scriptblock] $Block, [string] $ExpectedMessage, [string] $TestName)
  $actual = $null
  try {
    & $Block | Out-Null
  }
  catch {
    $actual = $_.Exception.Message
  }

  if ($actual -notlike "*$ExpectedMessage*") {
    $script:failures += "$TestName`n  expected error containing: $ExpectedMessage`n  actual:   $actual"
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

function New-ScratchDir {
  param([string] $Name)
  $dir = Join-Path ([IO.Path]::GetTempPath()) ("image-sources-test-" + $Name)
  if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
  New-Item -ItemType Directory -Path $dir -Force | Out-Null
  return $dir
}

function New-ImageFile {
  param([string] $Dir, [string] $FileName, [byte[]] $Bytes)
  $path = Join-Path $Dir $FileName
  [IO.File]::WriteAllBytes($path, $Bytes)
  return $path
}

# An IHDR follows the signature in a real PNG. The magic bytes are all detection reads.
$pngBytes = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D)
$icoBytes = [byte[]](0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x20, 0x20, 0x00, 0x00, 0x00, 0x00)
$svgText = "<?xml version=`"1.0`" encoding=`"UTF-8`"?>`n<svg xmlns=`"http://www.w3.org/2000/svg`" viewBox=`"0 0 100 40`"><rect width=`"100`" height=`"40`"/></svg>"

$scratch = New-ScratchDir -Name "kinds"

Assert-Equal -Expected "png" -Actual (Get-ImageKind -Path (New-ImageFile -Dir $scratch -FileName "no-extension" -Bytes $pngBytes)) -TestName "PNG signature is detected"
Assert-Equal -Expected "ico" -Actual (Get-ImageKind -Path (New-ImageFile -Dir $scratch -FileName "cursor.bin" -Bytes $icoBytes)) -TestName "ICO signature is detected"

$svgPath = New-ImageFile -Dir $scratch -FileName "lying-name.png" -Bytes ([Text.Encoding]::UTF8.GetBytes($svgText))
Assert-Equal -Expected "svg" -Actual (Get-ImageKind -Path $svgPath) -TestName "SVG text is detected from a .png file name"

$bomSvgPath = New-ImageFile -Dir $scratch -FileName "bom.svg" -Bytes (@(0xEF, 0xBB, 0xBF) + [Text.Encoding]::UTF8.GetBytes("`n`n" + $svgText))
Assert-Equal -Expected "svg" -Actual (Get-ImageKind -Path $bomSvgPath) -TestName "SVG behind a BOM and blank lines is detected"

$upperSvgPath = New-ImageFile -Dir $scratch -FileName "upper.svg" -Bytes ([Text.Encoding]::UTF8.GetBytes("<SVG xmlns=""http://www.w3.org/2000/svg""/>"))
Assert-Equal -Expected "svg" -Actual (Get-ImageKind -Path $upperSvgPath) -TestName "Uppercase SVG root element is detected"

Assert-Throws -Block { Get-ImageKind -Path (New-ImageFile -Dir $scratch -FileName "junk.txt" -Bytes ([Text.Encoding]::UTF8.GetBytes("not an image at all"))) } -ExpectedMessage "Unrecognized image format" -TestName "A payload that is no known format throws"
Assert-Throws -Block { Get-ImageKind -Path (New-ImageFile -Dir $scratch -FileName "empty.png" -Bytes ([byte[]]@())) } -ExpectedMessage "Unrecognized image format" -TestName "An empty payload throws"

# Staging renames to the detected kind, which is how the image tools downstream pick a decoder.
$stageDir = New-ScratchDir -Name "staging"
$mislabelled = New-ImageFile -Dir $stageDir -FileName "source.png" -Bytes ([Text.Encoding]::UTF8.GetBytes($svgText))
$staged = Get-SourceFile -Source $mislabelled -StagingDir $stageDir -BaseName "logo"
Assert-Equal -Expected (Join-Path $stageDir "logo.svg") -Actual $staged -TestName "An SVG source named .png stages as .svg"
Assert-Equal -Expected $true -Actual (Test-Path -LiteralPath $staged) -TestName "The staged file exists"
Assert-Equal -Expected 0 -Actual (@(Get-ChildItem -LiteralPath $stageDir -Filter "*.pending").Count) -TestName "No staging scratch file is left behind"

$pngStaged = Get-SourceFile -Source (New-ImageFile -Dir $stageDir -FileName "ico-source.png" -Bytes $icoBytes) -StagingDir $stageDir -BaseName "favicon"
Assert-Equal -Expected (Join-Path $stageDir "favicon.ico") -Actual $pngStaged -TestName "An ICO source named .png stages as .ico"
Assert-Equal -Expected "" -Actual (Get-SourceFile -Source "" -StagingDir $stageDir -BaseName "logo") -TestName "An empty source stages nothing"
Assert-Throws -Block { Get-SourceFile -Source (Join-Path $stageDir "does-not-exist.png") -StagingDir $stageDir -BaseName "logo" } -ExpectedMessage "Source not found" -TestName "A missing source throws"

# customize.ps1 has to reject a source whose bytes do not match the field it was passed to, and it
# must do that before reaching the image tools. The throw keeps these runs off dotnet.
function New-FakeRepo {
  param([string] $Name)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-image-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value 'namespace ControlR.Libraries.Branding;' -Encoding UTF8

  return $root
}

function Invoke-FakeRepoCustomize {
  param([string] $Root, [hashtable] $ExtraParams)
  $prevEnvFile = $env:GITHUB_ENV
  try {
    $env:GITHUB_ENV = Join-Path $Root "github-env.txt"
    & (Join-Path $Root ".scripts/customize.ps1") -SkipBuild -Version "1.2.3.4" @ExtraParams
  }
  finally {
    $env:GITHUB_ENV = $prevEnvFile
  }
}

$rejectRepo = New-FakeRepo -Name "reject"
$icoBytesPath = New-ImageFile -Dir (New-ScratchDir -Name "reject-source") -FileName "icon.png" -Bytes $icoBytes
$pngBytesPath = New-ImageFile -Dir $rejectRepo -FileName "logo.png" -Bytes $pngBytes

Assert-Throws -Block { Invoke-FakeRepoCustomize -Root $rejectRepo -ExtraParams @{ IconPng = $icoBytesPath } } -ExpectedMessage "Icon source must be PNG or SVG" -TestName "customize.ps1 rejects an ICO passed as the PNG icon"
Assert-Throws -Block { Invoke-FakeRepoCustomize -Root $rejectRepo -ExtraParams @{ IconIco = $pngBytesPath } } -ExpectedMessage "ICO source must be a real ICO file" -TestName "customize.ps1 rejects a PNG passed as the ICO icon"
Assert-Throws -Block { Invoke-FakeRepoCustomize -Root $rejectRepo -ExtraParams @{ CompanyLogoPng = $icoBytesPath } } -ExpectedMessage "Company logo source must be PNG or SVG" -TestName "customize.ps1 rejects an ICO passed as the company logo"

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
  exit 1
}

Write-Host "All image source tests passed." -ForegroundColor Green
exit 0
