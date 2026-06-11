[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Version,

  [Parameter()]
  [string] $ConfigUrl = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$customizeScript = Join-Path $PSScriptRoot "customize.ps1"

# Default brand values when no config URL is provided.
$brandName = "ControlR"
$brandKey = "ControlR"
$unixBrandKey = "controlr"

if ($ConfigUrl) {
  Write-Host "Downloading customization config from: $ConfigUrl"
  try {
    $config = Invoke-RestMethod -Uri $ConfigUrl -UseBasicParsing
  }
  catch {
    Write-Error "Failed to download or parse customization config: $_"
    throw
  }

  # Resolve brand name.
  if ($config.brandName) {
    $brandName = $config.brandName
  }

  # Compute sanitized keys from brand name.
  $brandKey = $brandName -replace '[^A-Za-z0-9]', '_'
  $unixBrandKey = $brandKey.ToLowerInvariant()

  # Build splat hashtable for customize.ps1.
  $customizeParams = @{
    BrandName = $brandName
    Version   = $Version
    SkipBuild = $true
    WhatIf    = $false
  }

  if ($config.publisher) {
    $customizeParams.Publisher = $config.publisher
  }

  # Unwrap colors from nested JSON.
  if ($config.colors) {
    $dark = $config.colors.dark
    if ($dark) {
      if ($dark.primary) { $customizeParams.PrimaryColorDark = $dark.primary }
      if ($dark.secondary) { $customizeParams.SecondaryColorDark = $dark.secondary }
      if ($dark.tertiary) { $customizeParams.TertiaryColorDark = $dark.tertiary }
      if ($dark.info) { $customizeParams.InfoColorDark = $dark.info }
      if ($dark.success) { $customizeParams.SuccessColorDark = $dark.success }
      if ($dark.warning) { $customizeParams.WarningColorDark = $dark.warning }
      if ($dark.error) { $customizeParams.ErrorColorDark = $dark.error }
    }

    $light = $config.colors.light
    if ($light) {
      if ($light.primary) { $customizeParams.PrimaryColorLight = $light.primary }
      if ($light.secondary) { $customizeParams.SecondaryColorLight = $light.secondary }
      if ($light.tertiary) { $customizeParams.TertiaryColorLight = $light.tertiary }
      if ($light.info) { $customizeParams.InfoColorLight = $light.info }
      if ($light.success) { $customizeParams.SuccessColorLight = $light.success }
      if ($light.warning) { $customizeParams.WarningColorLight = $light.warning }
      if ($light.error) { $customizeParams.ErrorColorLight = $light.error }
    }
  }

  if ($config.images) {
    if ($config.images.pngUri) { $customizeParams.IconPng = $config.images.pngUri }
    if ($config.images.icoUri) { $customizeParams.IconIco = $config.images.icoUri }
    if ($config.images.companyLogoPngUri) { $customizeParams.CompanyLogoPng = $config.images.companyLogoPngUri }
  }

  Write-Host "Applying customization for brand: $brandName"
  Write-Host "  BrandKey: $brandKey"
  Write-Host "  UnixBrandKey: $unixBrandKey"

  & $customizeScript @customizeParams

  if ($LASTEXITCODE -ne 0) {
    Write-Error "customize.ps1 exited with code $LASTEXITCODE"
    exit $LASTEXITCODE
  }

  Write-Host "Customization applied successfully."
}
else {
  Write-Host "No customization config URL provided. Using default branding: $brandName"
}

# Also export as job-level environment variables so shell steps can use $BRAND_KEY / $UNIX_BRAND_KEY.
Write-Output "BRAND_KEY=$brandKey" >> $env:GITHUB_ENV
Write-Output "UNIX_BRAND_KEY=$unixBrandKey" >> $env:GITHUB_ENV