# Functional tests for the assembly-name rebrand in customize.ps1.
# Verifies the InternalsVisibleTo grants follow the same rename rule as Directory.Build.props
# (ControlR.Web.* projects keep their assembly names) and that root dot-directories are skipped.
# Run: pwsh -File Test-CustomizeAssemblyRename.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$failures = @()

function Assert-Contains {
  param([string] $Content, [string] $Expected, [string] $TestName)
  if (-not $Content.Contains($Expected)) {
    $script:failures += "$TestName`n  expected to contain: $Expected`n  actual:   $Content"
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

function Assert-NotContains {
  param([string] $Content, [string] $Unexpected, [string] $TestName)
  if ($Content.Contains($Unexpected)) {
    $script:failures += "$TestName`n  expected NOT to contain: $Unexpected`n  actual:   $Content"
  }
  else {
    Write-Host "PASS: $TestName" -ForegroundColor Green
  }
}

# customize.ps1 locates the repo root via $PSScriptRoot/.., so the fake repo needs a .scripts
# copy of the real scripts plus the files the script edits before it reaches the SkipBuild exit.
function New-FakeRepo {
  param([string] $Name)
  $root = Join-Path ([IO.Path]::GetTempPath()) ("customize-rename-test-" + $Name)
  if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }

  New-Item -ItemType Directory -Path (Join-Path $root ".scripts") -Force | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot "../*.ps1") -Destination (Join-Path $root ".scripts") -Force

  Set-Content -Path (Join-Path $root "Directory.Build.props") -Value '<Project><PropertyGroup><BrandPrefix>ControlR</BrandPrefix></PropertyGroup></Project>' -Encoding UTF8

  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding") -Force | Out-Null
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs") -Value 'namespace ControlR.Libraries.Branding;' -Encoding UTF8

  # A web-owned grant that must survive untouched.
  New-Item -ItemType Directory -Path (Join-Path $root "ControlR.Web.Server/Properties") -Force | Out-Null
  Set-Content -Path (Join-Path $root "ControlR.Web.Server/Properties/AssemblyInfo.cs") -Value '[assembly: InternalsVisibleTo("ControlR.Web.Server.Tests")]' -Encoding UTF8

  # A grant that mixes a web target (unchanged) with a normal target (rebranded).
  New-Item -ItemType Directory -Path (Join-Path $root "Libraries/ControlR.Libraries.Shared/Properties") -Force | Out-Null
  $sharedInfo = @"
[assembly: InternalsVisibleTo("ControlR.Web.Server")]
[assembly: InternalsVisibleTo("ControlR.Agent.LoadTester")]
"@
  Set-Content -Path (Join-Path $root "Libraries/ControlR.Libraries.Shared/Properties/AssemblyInfo.cs") -Value $sharedInfo -Encoding UTF8

  # An avares URI that must be rebranded.
  New-Item -ItemType Directory -Path (Join-Path $root "ControlR.DesktopClient") -Force | Out-Null
  Set-Content -Path (Join-Path $root "ControlR.DesktopClient/App.axaml") -Value '<ResourceInclude Source="avares://ControlR.Libraries.Avalonia/Resources/Theme.axaml" />' -Encoding UTF8

  # Worktrees under root dot-directories must not be rewritten.
  New-Item -ItemType Directory -Path (Join-Path $root ".qwen/worktrees/other/Properties") -Force | Out-Null
  Set-Content -Path (Join-Path $root ".qwen/worktrees/other/Properties/AssemblyInfo.cs") -Value '[assembly: InternalsVisibleTo("ControlR.Web.Client.Tests")]' -Encoding UTF8
  New-Item -ItemType Directory -Path (Join-Path $root ".worktrees/other") -Force | Out-Null
  Set-Content -Path (Join-Path $root ".worktrees/other/App.axaml") -Value '<ResourceInclude Source="avares://ControlR.Libraries.Avalonia/Resources/Theme.axaml" />' -Encoding UTF8

  return $root
}

function Invoke-Customize {
  param([string] $Root)
  & (Join-Path $Root ".scripts/customize.ps1") -BrandName "Remote Support" -Version "1.2.3.4" -SkipBuild
}

function Get-FileContent {
  param([string] $Root, [string] $RelativePath)
  return Get-Content -LiteralPath (Join-Path $Root $RelativePath) -Raw -Encoding UTF8
}

# 'Remote Support' sanitizes to the brand key 'Remote_Support'.
$repo = New-FakeRepo -Name "assembly-rename"
Invoke-Customize -Root $repo

# A ControlR.Web.* target keeps its assembly name, so the grant must not be rebranded.
$webServerInfo = Get-FileContent -Root $repo -RelativePath "ControlR.Web.Server/Properties/AssemblyInfo.cs"
Assert-Contains -Content $webServerInfo -Expected 'InternalsVisibleTo("ControlR.Web.Server.Tests")' -TestName "A ControlR.Web.* grant keeps its target"
Assert-NotContains -Content $webServerInfo -Unexpected 'Remote_Support.Web.Server.Tests' -TestName "A ControlR.Web.* grant is not rebranded"

# A single file may hold both kinds of grants; each is treated on its own.
$sharedInfo = Get-FileContent -Root $repo -RelativePath "Libraries/ControlR.Libraries.Shared/Properties/AssemblyInfo.cs"
Assert-Contains -Content $sharedInfo -Expected 'InternalsVisibleTo("ControlR.Web.Server")' -TestName "A web target in a mixed file is left alone"
Assert-Contains -Content $sharedInfo -Expected 'InternalsVisibleTo("Remote_Support.Agent.LoadTester")' -TestName "A non-web target in a mixed file is rebranded"

# avares URIs reference renamed assemblies and must be rebranded.
$axaml = Get-FileContent -Root $repo -RelativePath "ControlR.DesktopClient/App.axaml"
Assert-Contains -Content $axaml -Expected 'avares://Remote_Support.Libraries.Avalonia/' -TestName "An avares URI is rebranded"

# Root dot-directories are skipped entirely.
$worktreeInfo = Get-FileContent -Root $repo -RelativePath ".qwen/worktrees/other/Properties/AssemblyInfo.cs"
Assert-Contains -Content $worktreeInfo -Expected 'InternalsVisibleTo("ControlR.Web.Client.Tests")' -TestName "An AssemblyInfo under a dot-directory is untouched"
$worktreeAxaml = Get-FileContent -Root $repo -RelativePath ".worktrees/other/App.axaml"
Assert-Contains -Content $worktreeAxaml -Expected 'avares://ControlR.Libraries.Avalonia/' -TestName "An axaml under a dot-directory is untouched"

if ($failures.Count -gt 0) {
  Write-Host ""
  Write-Host "$($failures.Count) test(s) failed:" -ForegroundColor Red
  $failures | ForEach-Object { Write-Host $_ }
  exit 1
}

Write-Host ""
Write-Host "All assembly rename script tests passed." -ForegroundColor Green
