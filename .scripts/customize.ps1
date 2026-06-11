[CmdletBinding()]
param(
  [Parameter(HelpMessage = "Display name shown in UI (e.g. 'Cool Company')")]
  [string] $BrandName = "ControlR",

  [Parameter(HelpMessage = "Publisher name for ARP entry, etc.")]
  [string] $Publisher = "Bitbound",

  [Parameter(HelpMessage = "Primary color hex for dark theme")]
  [string] $PrimaryColorDark = "#2196F3",

  [Parameter(HelpMessage = "Secondary color hex for dark theme")]
  [string] $SecondaryColorDark = "#21f3e9",

  [Parameter(HelpMessage = "Tertiary color hex for dark theme")]
  [string] $TertiaryColorDark = "#7b21f3",

  [Parameter(HelpMessage = "Info color hex for dark theme")]
  [string] $InfoColorDark = "#89b4f8",

  [Parameter(HelpMessage = "Success color hex for dark theme")]
  [string] $SuccessColorDark = "#2cb67d",

  [Parameter(HelpMessage = "Warning color hex for dark theme")]
  [string] $WarningColorDark = "#facc15",

  [Parameter(HelpMessage = "Error color hex for dark theme")]
  [string] $ErrorColorDark = "#f87171",

  [Parameter(HelpMessage = "Primary color hex for light theme")]
  [string] $PrimaryColorLight = "#2196F3",

  [Parameter(HelpMessage = "Secondary color hex for light theme")]
  [string] $SecondaryColorLight = "#008c7a",

  [Parameter(HelpMessage = "Tertiary color hex for light theme")]
  [string] $TertiaryColorLight = "#7b21f3",

  [Parameter(HelpMessage = "Info color hex for light theme")]
  [string] $InfoColorLight = "#0d6efd",

  [Parameter(HelpMessage = "Success color hex for light theme")]
  [string] $SuccessColorLight = "#28a745",

  [Parameter(HelpMessage = "Warning color hex for light theme")]
  [string] $WarningColorLight = "#ffc107",

  [Parameter(HelpMessage = "Error color hex for light theme")]
  [string] $ErrorColorLight = "#dc3545",

  [Parameter(HelpMessage = "Path or URL to master PNG icon (512px+)")]
  [string] $IconPng = "",

  [Parameter(HelpMessage = "Path or URL to ICO icon (multi-size)")]
  [string] $IconIco = "",

  [Parameter(HelpMessage = "Path or URL to company logo PNG (for email templates, etc.)")]
  [string] $CompanyLogoPng = "",

  [Parameter(HelpMessage = "Build version (e.g. '1.2.3.0')")]
  [string] $Version = "0.0.1.0",

  [Parameter(HelpMessage = "Output directory for final build artifacts")]
  [string] $OutputPath = "",

  [switch] $SkipBuild,

  [switch] $WhatIf
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$PrimaryColorDark = $PrimaryColorDark.TrimStart('#')
$SecondaryColorDark = $SecondaryColorDark.TrimStart('#')
$TertiaryColorDark = $TertiaryColorDark.TrimStart('#')
$InfoColorDark = $InfoColorDark.TrimStart('#')
$SuccessColorDark = $SuccessColorDark.TrimStart('#')
$WarningColorDark = $WarningColorDark.TrimStart('#')
$ErrorColorDark = $ErrorColorDark.TrimStart('#')
$PrimaryColorLight = $PrimaryColorLight.TrimStart('#')
$SecondaryColorLight = $SecondaryColorLight.TrimStart('#')
$TertiaryColorLight = $TertiaryColorLight.TrimStart('#')
$InfoColorLight = $InfoColorLight.TrimStart('#')
$SuccessColorLight = $SuccessColorLight.TrimStart('#')
$WarningColorLight = $WarningColorLight.TrimStart('#')
$ErrorColorLight = $ErrorColorLight.TrimStart('#')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$tempBuildRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) "ControlR-customize"

if (-not $OutputPath) {
  $OutputPath = Join-Path $tempBuildRoot "output"
}

#region Helpers

function Get-SourceFile {
  param([string] $Source, [string] $StagingDir, [string] $FileName)

  if (-not $Source) { return "" }

  $dest = Join-Path $StagingDir $FileName

  if ($Source -match "^https?://") {
    Write-Host "Downloading $Source -> $dest"
    Invoke-WebRequest -Uri $Source -OutFile $dest -UseBasicParsing
    return $dest
  }

  if ($Source -match "^\\\\") {
    Write-Host "Copying (UNC) $Source -> $dest"
    Copy-Item -LiteralPath $Source -Destination $dest -Force
    return $dest
  }

  if (Test-Path -LiteralPath $Source) {
    Write-Host "Copying $Source -> $dest"
    Copy-Item -LiteralPath $Source -Destination $dest -Force
    return $dest
  }

  throw "Source not found: $Source"
}

function Resize-Image {
  param(
    [string] $SourcePath,
    [string] $OutputPath,
    [int] $Width,
    [int] $Height
  )

  if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source image not found for resize: $SourcePath"
  }

  $csScriptPath = Join-Path $PSScriptRoot "resize-image.cs"
  dotnet run "$csScriptPath" "$SourcePath" "$OutputPath" $Width $Height
  if ($LASTEXITCODE -ne 0) {
    throw "Image resize failed for $SourcePath -> $OutputPath"
  }
}

function Update-FileContent {
  param(
    [string] $FilePath,
    [hashtable] $Replacements
  )

  $content = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
  $original = $content

  foreach ($key in $Replacements.Keys) {
    $content = $content.Replace($key, $Replacements[$key])
  }

  if ($content -ne $original) {
    Write-WhatIfDiff -OldContent $original -NewContent $content -FilePath $FilePath
    Write-FileContent -Path $FilePath -Content $content
  }
}

function Write-FileContent {
  param(
    [string] $Path,
    [string] $Content
  )

  if ($WhatIf) { return }

  $rawBytes = [System.IO.File]::ReadAllBytes($Path)
  $hasBom = $rawBytes.Length -ge 3 -and $rawBytes[0] -eq 0xEF -and $rawBytes[1] -eq 0xBB -and $rawBytes[2] -eq 0xBF
  $encoding = New-Object System.Text.UTF8Encoding($hasBom)
  [System.IO.File]::WriteAllText($Path, $Content, $encoding)
  Write-Host "Updated: $Path"
}

function Write-WhatIfDiff {
  param(
    [string] $OldContent,
    [string] $NewContent,
    [string] $FilePath
  )

  if (-not $WhatIf) { return }

  $oldLines = $OldContent -split "`n"
  $newLines = $NewContent -split "`n"
  $maxLen = [Math]::Max($oldLines.Length, $newLines.Length)
  $changed = @()

  for ($i = 0; $i -lt $maxLen; $i++) {
    $oldLine = if ($i -lt $oldLines.Length) { $oldLines[$i] } else { "" }
    $newLine = if ($i -lt $newLines.Length) { $newLines[$i] } else { "" }
    if ($oldLine -ne $newLine) {
      $changed += [PSCustomObject]@{ Line = $i + 1; Old = $oldLine; New = $newLine }
    }
  }

  if ($changed.Count -gt 0) {
    Write-Host "[What-If] Would change $FilePath ($($changed.Count) line(s)):" -ForegroundColor Gray
    foreach ($c in $changed) {
      $oldTrimmed = $c.Old.Trim().TrimEnd()
      $newTrimmed = $c.New.Trim().TrimEnd()
      if ($oldTrimmed -and $newTrimmed) {
        Write-Host "  [$($c.Line)] - '$oldTrimmed'" -ForegroundColor DarkGray
        Write-Host "  [$($c.Line)] + '$newTrimmed'" -ForegroundColor Green
      }
      elseif ($newTrimmed) {
        Write-Host "  [$($c.Line)] + '$newTrimmed'" -ForegroundColor Green
      }
      else {
        Write-Host "  [$($c.Line)] - '$oldTrimmed'" -ForegroundColor DarkGray
      }
    }
    Write-Host ""
  }
}

function Get-BlendedColor {
  param(
    [string] $Foreground,
    [string] $Background,
    [double] $Opacity
  )

  $fr = [int]::Parse($Foreground.Substring(0, 2), "HexNumber")
  $fg = [int]::Parse($Foreground.Substring(2, 2), "HexNumber")
  $fb = [int]::Parse($Foreground.Substring(4, 2), "HexNumber")
  $br = [int]::Parse($Background.Substring(0, 2), "HexNumber")
  $bg = [int]::Parse($Background.Substring(2, 2), "HexNumber")
  $bb = [int]::Parse($Background.Substring(4, 2), "HexNumber")

  $r = [int]($fr * $Opacity + $br * (1 - $Opacity))
  $g = [int]($fg * $Opacity + $bg * (1 - $Opacity))
  $b = [int]($fb * $Opacity + $bb * (1 - $Opacity))

  return "{0:X2}{1:X2}{2:X2}" -f $r, $g, $b
}

function Get-LighterColor {
  param(
    [string] $HexColor,
    [double] $Amount
  )

  $r = [int]::Parse($HexColor.Substring(0, 2), "HexNumber")
  $g = [int]::Parse($HexColor.Substring(2, 2), "HexNumber")
  $b = [int]::Parse($HexColor.Substring(4, 2), "HexNumber")

  $r = [int]([Math]::Round($r + (255 - $r) * $Amount))
  $g = [int]([Math]::Round($g + (255 - $g) * $Amount))
  $b = [int]([Math]::Round($b + (255 - $b) * $Amount))

  return "{0:X2}{1:X2}{2:X2}" -f $r, $g, $b
}

function Get-DarkerColor {
  param(
    [string] $HexColor,
    [double] $Amount
  )

  $r = [int]::Parse($HexColor.Substring(0, 2), "HexNumber")
  $g = [int]::Parse($HexColor.Substring(2, 2), "HexNumber")
  $b = [int]::Parse($HexColor.Substring(4, 2), "HexNumber")

  $r = [int]([Math]::Round($r * (1 - $Amount)))
  $g = [int]([Math]::Round($g * (1 - $Amount)))
  $b = [int]([Math]::Round($b * (1 - $Amount)))

  return "{0:X2}{1:X2}{2:X2}" -f $r, $g, $b
}

function Get-ContrastTextColor {
  param([string] $HexColor)

  $r = [int]::Parse($HexColor.Substring(0, 2), "HexNumber")
  $g = [int]::Parse($HexColor.Substring(2, 2), "HexNumber")
  $b = [int]::Parse($HexColor.Substring(4, 2), "HexNumber")

  $luminance = 0.299 * $r + 0.587 * $g + 0.114 * $b

  if ($luminance -gt 160) {
    return "000000"
  }
  return "FFFFFF"
}

function Set-ThemeBrush {
  param(
    [string] $Content,
    [string] $Key,
    [string] $Color
  )

  $pattern = '(<SolidColorBrush x:Key="' + [regex]::Escape($Key) + '" Color=")#[0-9A-Fa-f]+(")'
  $replacement = '${1}#' + $Color + '${2}'
  return $Content -replace $pattern, $replacement
}

#endregion

#region Validate Inputs

$hexPattern = "^[0-9A-Fa-f]{6}$"
$colorParams = @{
  PrimaryColorDark    = $PrimaryColorDark
  SecondaryColorDark  = $SecondaryColorDark
  TertiaryColorDark   = $TertiaryColorDark
  InfoColorDark       = $InfoColorDark
  SuccessColorDark    = $SuccessColorDark
  WarningColorDark    = $WarningColorDark
  ErrorColorDark      = $ErrorColorDark
  PrimaryColorLight   = $PrimaryColorLight
  SecondaryColorLight = $SecondaryColorLight
  TertiaryColorLight  = $TertiaryColorLight
  InfoColorLight      = $InfoColorLight
  SuccessColorLight   = $SuccessColorLight
  WarningColorLight   = $WarningColorLight
  ErrorColorLight     = $ErrorColorLight
}

foreach ($kv in $colorParams.GetEnumerator()) {
  if ($kv.Value -notmatch $hexPattern) {
    throw "Invalid hex color for $($kv.Key): '$($kv.Value)'. Expected 6-character hex (e.g. FF1122 or #FF1122)."
  }
}

if ($BrandName -notmatch "^[A-Za-z][A-Za-z0-9_ \-\.]*$") {
  throw "BrandName must start with a letter and contain only alphanumeric characters, underscores, hyphens, spaces, and periods."
}

if ($Publisher -notmatch "^[A-Za-z][A-Za-z0-9_ \-]*$") {
  throw "Publisher must start with a letter and contain only alphanumeric characters, underscores, hyphens, and spaces."
}

#endregion

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Customizing: $BrandName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

#region Update BrandPrefix in Directory.Build.props

$brandKey = $BrandName -replace '[^A-Za-z0-9]', '_'
$unixBrandKey = $BrandName.ToLowerInvariant() -replace '[^a-z0-9]', '_'
Write-Host "Updating BrandPrefix in Directory.Build.props" -ForegroundColor Yellow

$propsFile = Join-Path $repoRoot "Directory.Build.props"
$propsContent = Get-Content -LiteralPath $propsFile -Raw -Encoding UTF8
$propsNew = $propsContent -replace '<BrandPrefix>.*?</BrandPrefix>', "<BrandPrefix>$brandKey</BrandPrefix>"

if ($propsNew -ne $propsContent) {
  Write-WhatIfDiff -OldContent $propsContent -NewContent $propsNew -FilePath $propsFile
  Write-FileContent -Path $propsFile -Content $propsNew
}

#endregion

#region Update BrandingConstants.cs

Write-Host "Updating BrandingConstants.cs" -ForegroundColor Yellow

$brandingFile = Join-Path $repoRoot "Libraries/ControlR.Libraries.Branding/BrandingConstants.cs"
$original = Get-Content -LiteralPath $brandingFile -Raw -Encoding UTF8
$content = $original

$content = $content -replace 'public const string BrandName = ".*?";', "public const string BrandName = `"$BrandName`";"
$content = $content -replace 'public const string Publisher = ".*?";', "public const string Publisher = `"$Publisher`";"

$colorFields = @(
  "PrimaryColorDark", "SecondaryColorDark", "TertiaryColorDark",
  "InfoColorDark", "SuccessColorDark", "WarningColorDark", "ErrorColorDark",
  "PrimaryColorLight", "SecondaryColorLight", "TertiaryColorLight",
  "InfoColorLight", "SuccessColorLight", "WarningColorLight", "ErrorColorLight"
)

foreach ($field in $colorFields) {
  $value = $colorParams[$field]
  $content = $content -replace "(public const string $field)(\s*=\s*)`"[0-9A-Fa-f]+`"", "`$1`$2`"$value`""
}

if ($content -ne $original) {
  Write-WhatIfDiff -OldContent $original -NewContent $content -FilePath $brandingFile
  Write-FileContent -Path $brandingFile -Content $content
}

#endregion

#region Process Icons

Write-Host "Processing icons" -ForegroundColor Yellow

if ($WhatIf) {
  if ($IconPng) { Write-Host "[What-If] Would download/copy icon from: $IconPng and distribute to all icon locations" -ForegroundColor Gray }
  if ($IconIco) { Write-Host "[What-If] Would download/copy ICO from: $IconIco and distribute to all icon locations" -ForegroundColor Gray }
  if (-not $IconPng -and -not $IconIco) { Write-Host "[What-If] No icons provided (using defaults)" -ForegroundColor Gray }
  Write-Host ""
}
else {
  $stagingDir = Join-Path $tempBuildRoot "icon-staging"
  if (Test-Path -LiteralPath $stagingDir) { Remove-Item -LiteralPath $stagingDir -Recurse -Force }
  New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

  $masterPng = ""
  $masterIco = ""

  if ($IconPng) {
    $masterPng = Get-SourceFile -Source $IconPng -StagingDir $stagingDir -FileName "master.png"
  }
  if ($IconIco) {
    $masterIco = Get-SourceFile -Source $IconIco -StagingDir $stagingDir -FileName "master.ico"
  }

  if ($masterPng) {
    Write-Host "Distributing PNG icon to all locations"

    $pngLocations = @(
      ".assets/appicon.png"
      "ControlR.DesktopClient/Assets/appicon.png"
    )

    foreach ($loc in $pngLocations) {
      $dest = Join-Path $repoRoot $loc
      Copy-Item -LiteralPath $masterPng -Destination $dest -Force
      Write-Host "  -> $loc"
    }

    $macSizes = @(16, 32, 64, 128, 256, 512, 1024)
    $macBase = "ControlR.DesktopClient/Assets.xcassets/AppIcon.appiconset"
    foreach ($size in $macSizes) {
      Resize-Image -SourcePath $masterPng -OutputPath (Join-Path $repoRoot "$macBase/Icon$size.png") -Width $size -Height $size
    }

    Resize-Image -SourcePath $masterPng -OutputPath (Join-Path $repoRoot "ControlR.Web.Server/wwwroot/static/appicon-192.png") -Width 192 -Height 192
    Resize-Image -SourcePath $masterPng -OutputPath (Join-Path $repoRoot "ControlR.Web.Server/wwwroot/static/appicon-512.png") -Width 512 -Height 512
    Copy-Item -LiteralPath $masterPng -Destination (Join-Path $repoRoot "ControlR.Web.Server/wwwroot/static/appicon-transparent.png") -Force
  }

  if ($masterIco) {
    Write-Host "Distributing ICO icon to all locations"

    $icoLocations = @(
      ".assets/appicon.ico"
      "ControlR.DesktopClient/Assets/appicon.ico"
      "ControlR.Web.Server/wwwroot/static/favicon.ico"
    )

    foreach ($loc in $icoLocations) {
      $dest = Join-Path $repoRoot $loc
      Copy-Item -LiteralPath $masterIco -Destination $dest -Force
      Write-Host "  -> $loc"
    }
  }

  if ($masterPng -and -not $masterIco) {
    Write-Host "WARNING: No ICO provided. favicon.ico and appicon.ico locations not updated."
  }
  if ($masterIco -and -not $masterPng) {
    Write-Host "WARNING: No PNG provided. PNG-only locations not updated."
  }
}

#endregion

#region Process Company Logo

Write-Host "Processing company logo" -ForegroundColor Yellow

if ($WhatIf) {
  if ($CompanyLogoPng) { Write-Host "[What-If] Would download/copy company logo from: $CompanyLogoPng to wwwroot/images/company-logo.png" -ForegroundColor Gray }
  else { Write-Host "[What-If] No company logo provided (using default)" -ForegroundColor Gray }
  Write-Host ""
}
else {
  $stagingDir = Join-Path $tempBuildRoot "logo-staging"
  if (Test-Path -LiteralPath $stagingDir) { Remove-Item -LiteralPath $stagingDir -Recurse -Force }
  New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

  if ($CompanyLogoPng) {
    $masterLogo = Get-SourceFile -Source $CompanyLogoPng -StagingDir $stagingDir -FileName "company-logo.png"
    $destLogo = Join-Path $repoRoot "ControlR.Web.Server/wwwroot/images/company-logo.png"
    Copy-Item -LiteralPath $masterLogo -Destination $destLogo -Force
    Write-Host "  -> wwwroot/images/company-logo.png"
  }
}

#endregion

#region Update Info.plist macOS Bundle

Write-Host "Updating Info.plist" -ForegroundColor Yellow

$infoPlistFile = Join-Path $repoRoot "ControlR.DesktopClient/Info.plist"
if (Test-Path -LiteralPath $infoPlistFile) {
  $infoPlistContent = Get-Content -LiteralPath $infoPlistFile -Raw -Encoding UTF8
  $infoOriginal = $infoPlistContent

  $nl = [System.Environment]::NewLine

  $infoPlistContent = $infoPlistContent -replace '<string>ControlR</string>', "<string>$BrandName</string>"
  $infoPlistContent = $infoPlistContent -replace '<key>CFBundleExecutable</key>\s*<string>.*?</string>', "<key>CFBundleExecutable</key>$nl    <string>$brandKey.DesktopClient</string>"
  $infoPlistContent = $infoPlistContent -replace '<key>CFBundleIdentifier</key>\s*<string>.*?</string>', "<key>CFBundleIdentifier</key>$nl    <string>app.$unixBrandKey.desktop</string>"
  $infoPlistContent = $infoPlistContent -replace '<key>NSHumanReadableCopyright</key>\s*<string>.*?</string>', "<key>NSHumanReadableCopyright</key>$nl    <string>Copyright © $((Get-Date).Year) $Publisher. All rights reserved.</string>"
  $infoPlistContent = $infoPlistContent -replace '<string>ControlR uses notifications.*?</string>', "<string>$BrandName uses notifications to inform you about remote control sessions and system events.</string>"

  if ($infoPlistContent -ne $infoOriginal) {
    Write-WhatIfDiff -OldContent $infoOriginal -NewContent $infoPlistContent -FilePath $infoPlistFile
    Write-FileContent -Path $infoPlistFile -Content $infoPlistContent
  }
}

#endregion

#region Config / JSON Text Replacements

Write-Host "Updating config files" -ForegroundColor Yellow

Write-Host "Updating InternalsVisibleTo in AssemblyInfo.cs files" -ForegroundColor Yellow

$assemblyInfoFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter "AssemblyInfo.cs"
foreach ($aiFile in $assemblyInfoFiles) {
  $aiContent = Get-Content -LiteralPath $aiFile.FullName -Raw -Encoding UTF8
  $aiNew = $aiContent -replace '(InternalsVisibleTo\(")ControlR', "`${1}$brandKey"
  if ($aiNew -ne $aiContent) {
    Write-WhatIfDiff -OldContent $aiContent -NewContent $aiNew -FilePath $aiFile.FullName
    Write-FileContent -Path $aiFile.FullName -Content $aiNew
  }
}

Write-Host "Updating avares:// URIs in AXAML files" -ForegroundColor Yellow

$axamlFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter "*.axaml"
foreach ($axamlFile in $axamlFiles) {
  $axamlContent = Get-Content -LiteralPath $axamlFile.FullName -Raw -Encoding UTF8
  $axamlNew = $axamlContent -replace 'avares://ControlR\.', "avares://$brandKey."
  if ($axamlNew -ne $axamlContent) {
    Write-WhatIfDiff -OldContent $axamlContent -NewContent $axamlNew -FilePath $axamlFile.FullName
    Write-FileContent -Path $axamlFile.FullName -Content $axamlNew
  }
}

$configReplacements = @{
  '"AuthenticatorIssuerName": "ControlR"' = "`"AuthenticatorIssuerName`": `"$BrandName`""
}

$configFiles = @(
  "ControlR.Web.Server/appsettings.json"
  "ControlR.Web.Server/appsettings.Development.json"
)

foreach ($file in $configFiles) {
  $fullPath = Join-Path $repoRoot $file
  if (Test-Path -LiteralPath $fullPath) {
    Update-FileContent -FilePath $fullPath -Replacements $configReplacements
  }
}

$dockerReplacements = @{
  'ControlR_AppOptions__AuthenticatorIssuerName: "ControlR"' = "ControlR_AppOptions__AuthenticatorIssuerName: `"$BrandName`""
}

$dockerFiles = @(
  "docker-compose/docker-compose.yml"
  "docker-compose/docker-compose-secrets.yml"
)

foreach ($file in $dockerFiles) {
  $fullPath = Join-Path $repoRoot $file
  if (Test-Path -LiteralPath $fullPath) {
    Update-FileContent -FilePath $fullPath -Replacements $dockerReplacements
  }
}

$manifestFile = Join-Path $repoRoot "ControlR.Web.Server/wwwroot/static/manifest.webmanifest"
if (Test-Path -LiteralPath $manifestFile) {
  $manifestOriginal = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8
  $manifest = $manifestOriginal | ConvertFrom-Json
  $manifest.name = $BrandName
  $manifest.short_name = $BrandName
  $manifest.id = $brandKey.ToLowerInvariant()
  $manifest.theme_color = "#$PrimaryColorLight"
  $manifestNew = $manifest | ConvertTo-Json -Depth 5
  if ($manifestNew.Trim() -ne $manifestOriginal.Trim()) {
    Write-WhatIfDiff -OldContent $manifestOriginal -NewContent $manifestNew -FilePath $manifestFile
    Write-FileContent -Path $manifestFile -Content $manifestNew
  }
}

$localizationFile = Join-Path $repoRoot "ControlR.DesktopClient.Common/Resources/Strings/en-US.json"
if (Test-Path -LiteralPath $localizationFile) {
  $locReplacements = @{
    '"ControlR Chat"'                                                           = "`"$BrandName Chat`""
    '"ControlR is free, open-source, self-hostable, and powered by donations."' = "`"$BrandName is free, open-source, self-hostable, and powered by donations.`""
    '"ControlR uses the following open-source software and libraries."'         = "`"$BrandName uses the following open-source software and libraries.`""
  }
  Update-FileContent -FilePath $localizationFile -Replacements $locReplacements
}

$agentServiceTemplate = Join-Path $repoRoot "ControlR.Agent.Shared/Resources/controlr.agent.service"
if (Test-Path -LiteralPath $agentServiceTemplate) {
  $templateReplacements = @{
    "ControlR is an open-source remote control and remote access solution." = "$BrandName is an open-source remote control and remote access solution."
  }
  Update-FileContent -FilePath $agentServiceTemplate -Replacements $templateReplacements
}

$desktopServiceTemplate = Join-Path $repoRoot "ControlR.Agent.Shared/Resources/controlr.desktop.service"
if (Test-Path -LiteralPath $desktopServiceTemplate) {
  $templateReplacements = @{
    "ControlR Desktop Client provides user session interface for remote control." = "$BrandName Desktop Client provides user session interface for remote control."
  }
  Update-FileContent -FilePath $desktopServiceTemplate -Replacements $templateReplacements
}

$openApiFile = Join-Path $repoRoot "ControlR.Web.Server/ControlR.Web.Server.json"
if (Test-Path -LiteralPath $openApiFile) {
  $openApiReplacements = @{
    '"ControlR.Web.Server | v1"' = "`"$brandKey.Web.Server | v1`""
    '"ControlR.Web.Server"'      = "`"$brandKey.Web.Server`""
  }
  Update-FileContent -FilePath $openApiFile -Replacements $openApiReplacements
}

$installerProgramFile = Join-Path $repoRoot "ControlR.Agent.Installer/Program.cs"
if (Test-Path -LiteralPath $installerProgramFile) {
  $installerProgramContent = Get-Content -LiteralPath $installerProgramFile -Raw -Encoding UTF8
  $installerOriginal = $installerProgramContent

  $installerProgramContent = $installerProgramContent `
    -Replace ('const string RootDescription = ".*?"', "const string RootDescription = `"$BrandName agent installer.`"") `
    -Replace ('const string InstallCommandDescription = ".*?"', "const string InstallCommandDescription = `"Install the $BrandName agent bundle.`"") `
    -Replace ('const string UninstallCommandDescription = ".*?"', "const string UninstallCommandDescription = `"Uninstall the $BrandName agent bundle.`"") `
    -Replace ('const string TempDirectoryPrefix = ".*?"', "const string TempDirectoryPrefix = `"$unixBrandKey-install-`"") `
    -Replace ('const string TempBundleFileName = ".*?"', "const string TempBundleFileName = `"$brandKey.Agent.bundle.zip`"") `
    -Replace ('example\.\w+\.app', "example.$unixBrandKey.app")

  if ($installerProgramContent -ne $installerOriginal) {
    Write-WhatIfDiff -OldContent $installerOriginal -NewContent $installerProgramContent -FilePath $installerProgramFile
    Write-FileContent -Path $installerProgramFile -Content $installerProgramContent
  }
}

#endregion

#region CSS Color Replacements

Write-Host "Updating CSS colors" -ForegroundColor Yellow

$welcomeCss = Join-Path $repoRoot "ControlR.Web.Client/Components/Welcome.razor.css"
if (Test-Path -LiteralPath $welcomeCss) {
  $cssReplacements = @{
    "linear-gradient(90deg, #2196F3, #21f3e9, #7b21f3)" = "linear-gradient(90deg, #$PrimaryColorDark, #$SecondaryColorDark, #$TertiaryColorDark)"
    "linear-gradient(135deg, #2196F3, #21f3e9)"         = "linear-gradient(135deg, #$PrimaryColorDark, #$SecondaryColorDark)"
    "rgba(33, 150, 243, 0.3)"                           = "rgba($([int]::Parse($PrimaryColorDark.Substring(0,2), 'HexNumber')), $([int]::Parse($PrimaryColorDark.Substring(2,2), 'HexNumber')), $([int]::Parse($PrimaryColorDark.Substring(4,2), 'HexNumber')), 0.3)"
  }
  Update-FileContent -FilePath $welcomeCss -Replacements $cssReplacements
}

$notFoundCss = Join-Path $repoRoot "ControlR.Web.Client/Components/Pages/NotFound.razor.css"
if (Test-Path -LiteralPath $notFoundCss) {
  $cssReplacements = @{
    "linear-gradient(135deg, #2196F3, #21f3e9)" = "linear-gradient(135deg, #$PrimaryColorDark, #$SecondaryColorDark)"
  }
  Update-FileContent -FilePath $notFoundCss -Replacements $cssReplacements
}

$chatCss = Join-Path $repoRoot "ControlR.Web.Client/Components/Pages/DeviceAccess/Chat.razor.css"
if (Test-Path -LiteralPath $chatCss) {
  $darkBlended = Get-BlendedColor -Foreground $PrimaryColorDark -Background "121212" -Opacity 0.3
  $lightBlended = Get-BlendedColor -Foreground $PrimaryColorLight -Background "FFFFFF" -Opacity 0.35
  $chatCssReplacements = @{
    "#163A55" = "#$darkBlended"
    "#B1DAFB" = "#$lightBlended"
  }
  Update-FileContent -FilePath $chatCss -Replacements $chatCssReplacements
}

#endregion

#region AXAML Theme Replacements

Write-Host "Updating AXAML theme colors" -ForegroundColor Yellow

$themeAxaml = Join-Path $repoRoot "Libraries/ControlR.Libraries.Avalonia/Resources/Theme.axaml"
if (Test-Path -LiteralPath $themeAxaml) {
  $axamlOriginal = Get-Content -LiteralPath $themeAxaml -Raw -Encoding UTF8
  $axamlContent = $axamlOriginal

  $darkStartTag = '<ResourceDictionary x:Key="Dark">'
  $lightStartTag = '<ResourceDictionary x:Key="Light">'

  $darkStart = $axamlContent.IndexOf($darkStartTag)
  $lightStart = $axamlContent.IndexOf($lightStartTag)

  if ($darkStart -ge 0 -and $lightStart -ge 0) {
    $darkCloseTag = '</ResourceDictionary>'
    $darkEnd = $axamlContent.LastIndexOf($darkCloseTag, $lightStart) + $darkCloseTag.Length

    $themeDictCloseTag = '</ResourceDictionary.ThemeDictionaries>'
    $themeDictClose = $axamlContent.IndexOf($themeDictCloseTag)
    $lightEnd = $axamlContent.LastIndexOf($darkCloseTag, $themeDictClose) + $darkCloseTag.Length

    $preamble = $axamlContent.Substring(0, $darkStart)
    $darkSection = $axamlContent.Substring($darkStart, $darkEnd - $darkStart)
    $between = $axamlContent.Substring($darkEnd, $lightStart - $darkEnd)
    $lightSection = $axamlContent.Substring($lightStart, $lightEnd - $lightStart)
    $postamble = $axamlContent.Substring($lightEnd)

    $darkSemanticColors = @(
      @{ Name = "Primary"; Hex = $PrimaryColorDark }
      @{ Name = "Secondary"; Hex = $SecondaryColorDark }
      @{ Name = "Tertiary"; Hex = $TertiaryColorDark }
      @{ Name = "Info"; Hex = $InfoColorDark }
      @{ Name = "Success"; Hex = $SuccessColorDark }
      @{ Name = "Warning"; Hex = $WarningColorDark }
      @{ Name = "Error"; Hex = $ErrorColorDark }
    )

    $lightSemanticColors = @(
      @{ Name = "Primary"; Hex = $PrimaryColorLight }
      @{ Name = "Secondary"; Hex = $SecondaryColorLight }
      @{ Name = "Tertiary"; Hex = $TertiaryColorLight }
      @{ Name = "Info"; Hex = $InfoColorLight }
      @{ Name = "Success"; Hex = $SuccessColorLight }
      @{ Name = "Warning"; Hex = $WarningColorLight }
      @{ Name = "Error"; Hex = $ErrorColorLight }
    )

    foreach ($sc in $darkSemanticColors) {
      $base = $sc.Hex
      $contrastText = Get-ContrastTextColor -HexColor $base
      $filledHover = Get-LighterColor -HexColor $base -Amount 0.12
      $filledPressed = Get-LighterColor -HexColor $base -Amount 0.20

      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)Color" -Color $base
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)ContrastText" -Color $contrastText
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)HoverBrush" -Color "33$base"
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)PressedBrush" -Color "55$base"
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)FilledHoverBrush" -Color $filledHover
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)FilledPressedBrush" -Color $filledPressed
      $darkSection = Set-ThemeBrush -Content $darkSection -Key "$($sc.Name)DisabledBrush" -Color "1F$base"
    }

    foreach ($sc in $lightSemanticColors) {
      $base = $sc.Hex
      $contrastText = Get-ContrastTextColor -HexColor $base
      $filledHover = Get-DarkerColor -HexColor $base -Amount 0.08
      $filledPressed = Get-DarkerColor -HexColor $base -Amount 0.16

      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)Color" -Color $base
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)ContrastText" -Color $contrastText
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)HoverBrush" -Color "33$base"
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)PressedBrush" -Color "55$base"
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)FilledHoverBrush" -Color $filledHover
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)FilledPressedBrush" -Color $filledPressed
      $lightSection = Set-ThemeBrush -Content $lightSection -Key "$($sc.Name)DisabledBrush" -Color "1F$base"
    }

    $darkChatUser = Get-BlendedColor -Foreground $PrimaryColorDark -Background "121212" -Opacity 0.3
    $lightChatUser = Get-BlendedColor -Foreground $PrimaryColorLight -Background "FFFFFF" -Opacity 0.35
    $darkSection = Set-ThemeBrush -Content $darkSection -Key "ChatUserMessageBrush" -Color $darkChatUser
    $lightSection = Set-ThemeBrush -Content $lightSection -Key "ChatUserMessageBrush" -Color $lightChatUser

    $axamlContent = $preamble + $darkSection + $between + $lightSection + $postamble
  }
  else {
    Write-Warning "Could not find Dark/Light sections in Theme.axaml. Skipping."
  }

  if ($axamlContent -ne $axamlOriginal) {
    Write-WhatIfDiff -OldContent $axamlOriginal -NewContent $axamlContent -FilePath $themeAxaml
    Write-FileContent -Path $themeAxaml -Content $axamlContent
  }
}

$appAxaml = Join-Path $repoRoot "ControlR.DesktopClient/App.axaml"
if (Test-Path -LiteralPath $appAxaml) {
  $appOriginal = Get-Content -LiteralPath $appAxaml -Raw -Encoding UTF8
  $appContent = $appOriginal

  $darkAccentFirst = '(<ColorPaletteResources\s[^>]*?\bAccent=")[0-9A-Fa-f]+([^>]*?\bx:Key="Dark"[^>]*?>)'
  $darkKeyFirst = '(<ColorPaletteResources\s[^>]*?\bx:Key="Dark"[^>]*?\bAccent=")#[0-9A-Fa-f]+'
  $lightAccentFirst = '(<ColorPaletteResources\s[^>]*?\bAccent=")[0-9A-Fa-f]+([^>]*?\bx:Key="Light"[^>]*?>)'
  $lightKeyFirst = '(<ColorPaletteResources\s[^>]*?\bx:Key="Light"[^>]*?\bAccent=")#[0-9A-Fa-f]+'

  $appContent = $appContent -replace $darkAccentFirst, "`$1#$PrimaryColorDark`$2"
  $appContent = $appContent -replace $darkKeyFirst, "`$1#$PrimaryColorDark"
  $appContent = $appContent -replace $lightAccentFirst, "`$1#$PrimaryColorLight`$2"
  $appContent = $appContent -replace $lightKeyFirst, "`$1#$PrimaryColorLight"

  if ($appContent -ne $appOriginal) {
    Write-WhatIfDiff -OldContent $appOriginal -NewContent $appContent -FilePath $appAxaml
    Write-FileContent -Path $appAxaml -Content $appContent
  }
}

#endregion

if ($SkipBuild) {
  Write-Host ""
  Write-Host "Customization complete (build skipped)." -ForegroundColor Green
  exit 0
}

#region Build

Write-Host "Building solution" -ForegroundColor Yellow

if ($WhatIf) {
  Write-Host "[What-If] Would build DesktopClient, Agent, and Installer for: win-x64, win-x86, linux-x64" -ForegroundColor Gray
  Write-Host "[What-If] Would create bundle ZIPs and copy to wwwroot/downloads" -ForegroundColor Gray
  exit 0
}

$downloadsBase = Join-Path $repoRoot "ControlR.Web.Server/wwwroot/downloads"

$winArchitectures = @("win-x64", "win-x86")
$linuxArch = "linux-x64"

foreach ($arch in $winArchitectures) {
  Write-Host "Building DesktopClient ($arch)..." -ForegroundColor Cyan
  dotnet publish ControlR.DesktopClient/ -c Release -f net10.0-windows8.0 -r $arch --self-contained -o "ControlR.DesktopClient/bin/publish/$arch/" -p:Version=$Version -p:FileVersion=$Version
  if ($LASTEXITCODE -ne 0) { throw "DesktopClient build failed for $arch" }

  Write-Host "Building Agent ($arch)..." -ForegroundColor Cyan
  dotnet publish ControlR.Agent/ -c Release -r $arch -o "ControlR.Agent/bin/publish/$arch/" -p:PublishSingleFile=true -p:UseAppHost=true -p:Version=$Version -p:FileVersion=$Version -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true -p:DebugType=none
  if ($LASTEXITCODE -ne 0) { throw "Agent build failed for $arch" }

  Write-Host "Building Installer ($arch)..." -ForegroundColor Cyan
  $archDownloadDir = "$downloadsBase/$arch"
  New-Item -ItemType Directory -Path $archDownloadDir -Force | Out-Null
  dotnet publish ControlR.Agent.Installer/ -c Release -r $arch --self-contained -o $archDownloadDir -p:PublishSingleFile=true -p:UseAppHost=true -p:EnableCompressionInSingleFile=true -p:Version=$Version -p:FileVersion=$Version -p:DebugType=none
  if ($LASTEXITCODE -ne 0) { throw "Installer build failed for $arch" }

  Write-Host "Creating bundle ZIP ($arch)..." -ForegroundColor Cyan
  $bundleDir = Join-Path $tempBuildRoot "bundle/$arch"
  $desktopDir = Join-Path $bundleDir "DesktopClient"
  Remove-Item -LiteralPath $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Path $desktopDir -Force | Out-Null

  Copy-Item (Join-Path $repoRoot "ControlR.Agent/bin/publish/$arch/$brandKey.Agent.exe") (Join-Path $bundleDir "$brandKey.Agent.exe") -Force
  Copy-Item (Join-Path $repoRoot "ControlR.DesktopClient/bin/publish/$arch/*") $desktopDir -Recurse -Force

  $bundleZip = Join-Path $archDownloadDir "$brandKey.Agent.bundle.zip"
  if (Test-Path -LiteralPath $bundleZip) { Remove-Item -LiteralPath $bundleZip -Force }
  Compress-Archive -Path "$bundleDir/*" -DestinationPath $bundleZip -Force
}

Write-Host "Building DesktopClient ($linuxArch)..." -ForegroundColor Cyan
dotnet publish ControlR.DesktopClient/ -c Release -f net10.0 -r $linuxArch --self-contained -o "ControlR.DesktopClient/bin/publish/$linuxArch/" -p:Version=$Version -p:FileVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "DesktopClient build failed for $linuxArch" }

Write-Host "Building Agent ($linuxArch)..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path "ControlR.Agent/bin/publish/$linuxArch" -Force | Out-Null
dotnet publish ControlR.Agent/ -c Release -r $linuxArch -o "ControlR.Agent/bin/publish/$linuxArch/" -p:PublishSingleFile=true -p:UseAppHost=true -p:Version=$Version -p:FileVersion=$Version -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Agent build failed for $linuxArch" }

Write-Host "Building Installer ($linuxArch)..." -ForegroundColor Cyan
$linuxDownloadDir = "$downloadsBase/$linuxArch"
New-Item -ItemType Directory -Path $linuxDownloadDir -Force | Out-Null
dotnet publish ControlR.Agent.Installer/ -c Release -r $linuxArch --self-contained -o $linuxDownloadDir -p:PublishSingleFile=true -p:UseAppHost=true -p:EnableCompressionInSingleFile=true -p:Version=$Version -p:FileVersion=$Version -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Installer build failed for $linuxArch" }

Write-Host "Creating bundle ZIP ($linuxArch)..." -ForegroundColor Cyan
$linuxBundleDir = Join-Path $tempBuildRoot "bundle/$linuxArch"
$linuxDesktopDir = Join-Path $linuxBundleDir "DesktopClient"
Remove-Item -LiteralPath $linuxBundleDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $linuxDesktopDir -Force | Out-Null

Copy-Item (Join-Path $repoRoot "ControlR.Agent/bin/publish/$linuxArch/$brandKey.Agent") (Join-Path $linuxBundleDir "$brandKey.Agent") -Force
Copy-Item (Join-Path $repoRoot "ControlR.DesktopClient/bin/publish/$linuxArch/*") $linuxDesktopDir -Recurse -Force

$linuxBundleZip = Join-Path $linuxDownloadDir "$brandKey.Agent.bundle.zip"
if (Test-Path -LiteralPath $linuxBundleZip) { Remove-Item -LiteralPath $linuxBundleZip -Force }
Compress-Archive -Path "$linuxBundleDir/*" -DestinationPath $linuxBundleZip -Force

Write-Host "Creating Version.txt" -ForegroundColor Cyan
Set-Content -LiteralPath "$downloadsBase/Version.txt" -Value $Version -Encoding UTF8 -Force

Write-Host "Publishing Web Server..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path "ControlR.Web.Server/bin/publish" -Force | Out-Null
dotnet publish ControlR.Web.Server/ -p:ExcludeApp_Data=true --runtime linux-x64 --configuration Release -p:Version=$Version -p:FileVersion=$Version --output ControlR.Web.Server/bin/publish --self-contained true
if ($LASTEXITCODE -ne 0) { throw "Web Server publish failed" }

Write-Host "Verifying artifacts..." -ForegroundColor Cyan
$requiredArtifacts = @(
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/Version.txt"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/win-x64/$brandKey.Agent.Installer.exe"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/win-x86/$brandKey.Agent.Installer.exe"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/win-x64/$brandKey.Agent.bundle.zip"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/win-x86/$brandKey.Agent.bundle.zip"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/linux-x64/$brandKey.Agent.Installer"
  "ControlR.Web.Server/bin/publish/wwwroot/downloads/linux-x64/$brandKey.Agent.bundle.zip"
)

foreach ($artifact in $requiredArtifacts) {
  $fullPath = Join-Path $repoRoot $artifact
  if (-not (Test-Path -LiteralPath $fullPath)) {
    throw "Missing artifact: $artifact"
  }
  Write-Host "  OK: $artifact"
}

Write-Host "Creating Dockerfile" -ForegroundColor Cyan
$dockerfilePath = Join-Path $repoRoot "ControlR.Web.Server/bin/publish/Dockerfile"
@"
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt update
RUN apt -y install curl

USER `$APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY . /app

ENTRYPOINT ["dotnet", "ControlR.Web.Server.dll"]

HEALTHCHECK \
  CMD curl -f http://localhost:8080/health || exit 1
"@ | Set-Content -LiteralPath $dockerfilePath -Encoding UTF8

Write-Host "Creating final ZIP" -ForegroundColor Cyan
$outputDir = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$finalZip = Join-Path $outputDir "$brandKey-server-v$Version.zip"
if (Test-Path -LiteralPath $finalZip) { Remove-Item -LiteralPath $finalZip -Force }
Compress-Archive -Path (Join-Path $repoRoot "ControlR.Web.Server/bin/publish/*") -DestinationPath $finalZip -Force
Write-Host "Output: $finalZip"

#endregion

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Customization complete!" -ForegroundColor Green
Write-Host " Output: $finalZip" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
