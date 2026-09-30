# Selects the installed Xcode bundle that matches the pinned macOS workload band.
#
# GitHub-hosted images install each Xcode under a real directory (Xcode_26.0.1.app)
# plus name-only symlinks (Xcode_26.0.app, Xcode.app). The .NET macOS workload
# resolves actool through the selected path and breaks when that path runs through
# a symlink, so the chosen bundle is always resolved to its real path first.
# See dotnet/macios#21762.
#
# Self-hosted Macs install bundles with xcodes naming (Xcode-26.0.1.app). Bundle
# names differ by installer, so bundles are matched by the version each one reports
# rather than by name, and the highest patch inside the band wins.

[CmdletBinding()]
param(
  [string]$PropsPath = (Join-Path $PSScriptRoot '..' 'Directory.Build.props'),
  [string]$ApplicationsPath = '/Applications'
)

$ErrorActionPreference = 'Stop'

# Follows a path through symbolic links until it reaches a real file or directory.
# Returns the input path unchanged when nothing along it is a link. A relative link
# target is resolved against the link's own directory, which is how the filesystem
# reads it; resolving it against the process working directory would miss the link
# and hand back a symlinked path.
function Resolve-RealPath {
  param([Parameter(Mandatory)][string]$Path)

  $current = [System.IO.Path]::GetFullPath($Path)
  # Bounded so a link cycle cannot spin forever.
  for ($depth = 0; $depth -lt 32; $depth++) {
    $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
    if (-not $item) { return $current }
    if (-not $item.LinkType) { return $item.FullName }

    $target = @($item.Target)[0]
    if ([string]::IsNullOrWhiteSpace($target)) { return $item.FullName }

    $current = if ([System.IO.Path]::IsPathRooted($target)) {
      [System.IO.Path]::GetFullPath($target)
    } else {
      $parent = [System.IO.Path]::GetDirectoryName($item.FullName)
      [System.IO.Path]::GetFullPath((Join-Path $parent $target))
    }
  }
  return $current
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
# Real bundle paths already recorded, so several version-named symlinks onto one bundle
# are collapsed into a single candidate.
$seenPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
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
  if ($majorMinor -eq $band -and $seenPaths.Add($realApp)) {
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

# The runner's active Xcode is a version-named bundle on the hosted image, and that
# bundle is a symlink. xcode-select stores whatever path it is given, so leaving it
# pointed at a symlink keeps xcrun -find resolving through it. Repoint it at the real
# bundle. A no-op when xcode-select already points at the bundle we picked.
if ($IsMacOS) {
  $activeDir = (& /usr/bin/xcode-select -p 2>$null | Out-String).Trim()
  if ($activeDir -and ($activeDir -ne $developerDir)) {
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
