# Selects the installed Xcode bundle that matches the pinned macOS workload band.
#
# GitHub-hosted images install each major.minor Xcode under a real directory
# (Xcode_26.0.1.app) plus a name-only symlink (Xcode_26.0.app). The .NET macOS
# workload resolves actool through that path, and it breaks when the selected
# Xcode sits behind a symlink. So we always hand back the real path of a real
# bundle and never a symlink. See dotnet/macios#21762.
#
# Self-hosted Macs install bundles with xcodes naming (Xcode-26.0.1.app). Bundle
# names differ by installer, so we match the version a bundle reports rather than
# its name and pick the highest patch inside the band.

[CmdletBinding()]
param(
  [string]$PropsPath = (Join-Path $PSScriptRoot '..' 'Directory.Build.props'),
  [string]$ApplicationsPath = '/Applications'
)

$ErrorActionPreference = 'Stop'

# Resolve symlinks in a path (all components, not just the leaf) without
# requiring the path to exist first.
function Resolve-RealPath {
  param([Parameter(Mandatory)][string]$Path, [switch]$LeafOnly)

  $resolved = [System.IO.Path]::GetFullPath($Path)
  if ($LeafOnly) {
    $item = Get-Item -LiteralPath $resolved -Force -ErrorAction SilentlyContinue
    if ($item) { return $item.FullName }
    return $resolved
  }

  $item = Get-Item -LiteralPath $resolved -Force -ErrorAction SilentlyContinue
  if (-not $item) { return $resolved }

  $current = $item
  while ($current -and $current.PSObject.Properties['LinkType'] -and $current.LinkType) {
    $current = $current.Target | Select-Object -First 1
    $current = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
  }
  if ($current) { return $current.FullName }
  return $item.FullName
}

if (-not (Test-Path -LiteralPath $PropsPath)) {
  throw "Directory.Build.props not found at '$PropsPath'."
}

[xml]$props = Get-Content -Raw $PropsPath
$band = $props.SelectSingleNode('//MacTargetPlatformVersion')?.InnerText?.Trim()
if ([string]::IsNullOrWhiteSpace($band)) {
  throw "Could not read MacTargetPlatformVersion from '$PropsPath'."
}

$bandMatches = @()
$found = @()
foreach ($app in @(Get-ChildItem -LiteralPath $ApplicationsPath -Directory -Filter 'Xcode*.app' -ErrorAction SilentlyContinue)) {
  # Resolve the bundle symlink to a real bundle. A bundle that resolves to itself
  # is already real.
  $realApp = Resolve-RealPath -Path $app.FullName

  $xcodebuild = Join-Path $realApp 'Contents/Developer/usr/bin/xcodebuild'
  if (-not (Test-Path -LiteralPath $xcodebuild)) { continue }

  $report = & $xcodebuild -version 2>$null | Out-String
  if ($report -notmatch 'Xcode\s+([\d.]+)') { continue }

  $version = $Matches[1]
  $found += "$($app.Name) -> Xcode $version ($realApp)"
  $majorMinor = ($version.TrimEnd('.') -split '\.')[0..1] -join '.'
  # A resolved bundle is used by its real name only once, even though several
  # version-named symlinks can point at it.
  if ($majorMinor -eq $band -and $realApp -notin $bandMatches.Path) {
    $bandMatches += [pscustomobject]@{ Version = $version; Path = $realApp }
  }
}

if ($bandMatches.Count -eq 0) {
  $found | ForEach-Object { Write-Host $_ }
  Get-ChildItem -LiteralPath $ApplicationsPath -Filter 'Xcode*' -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_.Name }
  throw "No installed Xcode reports version $band.x."
}

$xc = ($bandMatches | Sort-Object { [version]$_.Version } | Select-Object -Last 1).Path
$developerDir = Join-Path $xc 'Contents/Developer'

# Point the toolchain at the chosen Xcode for every later step in the job. The
# current process needs it too, so local runs behave the same as CI runs.
if ($env:GITHUB_ENV) {
  "DEVELOPER_DIR=$developerDir" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
}
$env:DEVELOPER_DIR = $developerDir

# The runner's active Xcode may itself be a symlink (the hosted image points
# xcode-select at /Applications/Xcode.app). xcode-select stores whatever path it
# is given, so xcrun -find would keep resolving through a symlink. Repoint it at
# the real bundle. A no-op when the active bundle is already real.
if ($IsMacOS) {
  $activeDir = (& /usr/bin/xcode-select -p 2>$null | Out-String).Trim()
  $activeBrand = if ($activeDir) { Resolve-RealPath -Path $activeDir -LeafOnly } else { '' }
  if ($activeBrand -and ($activeBrand -ne $developerDir)) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & sudo /usr/bin/xcode-select --switch $developerDir *> $null
    $switchExit = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($switchExit -ne 0) {
      Write-Warning "Could not repoint xcode-select at $developerDir. Continuing with DEVELOPER_DIR."
    } else {
      Write-Host "Selected Xcode: $developerDir"
    }
  } else {
    Write-Host "Selected Xcode: $developerDir"
  }
}

& (Join-Path $developerDir 'usr/bin/xcodebuild') -version
