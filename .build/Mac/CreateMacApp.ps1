param (
  [Parameter(Mandatory=$true)]
  [string]$OutputDir
)

if (!(Test-Path $OutputDir)) {
    Write-Host "Output directory does not exist: $OutputDir"
    exit 1
}

Write-Host "Creating Mac app bundle in $OutputDir"

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Define paths
$AppName = "ControlR.app"
$AppPath = Join-Path $OutputDir $AppName
$ContentsPath = Join-Path $AppPath "Contents"
$MacOSPath = Join-Path $ContentsPath "MacOS"
$ResourcesPath = Join-Path $ContentsPath "Resources"
$InfoPlistSource = Join-Path $ScriptDir "Info.plist"
$IconSource = Join-Path (Split-Path (Split-Path $ScriptDir -Parent) -Parent) ".assets\appicon.icns"
if (-not (Test-Path $IconSource)) { 
    Write-Error "Required .assets appicon.icns not found at $IconSource"; 
    exit 1 
}

# Clean up existing app bundle if it exists
if (Test-Path $AppPath) {
    Write-Host "Removing existing app bundle: $AppPath"
    Remove-Item -Path $AppPath -Recurse -Force
}

# Create app bundle directory structure
Write-Host "Creating app bundle structure..."
New-Item -Path $ContentsPath -ItemType Directory -Force | Out-Null
New-Item -Path $MacOSPath -ItemType Directory -Force | Out-Null
New-Item -Path $ResourcesPath -ItemType Directory -Force | Out-Null

# Copy Info.plist
Write-Host "Copying Info.plist..."
if (Test-Path $InfoPlistSource) {
    Copy-Item -Path $InfoPlistSource -Destination (Join-Path $ContentsPath "Info.plist")
} else {
    Write-Host "Warning: Info.plist not found at $InfoPlistSource"
}

# Copy icon file
Write-Host "Copying app icon..."
if (Test-Path $IconSource) {
    Copy-Item -Path $IconSource -Destination (Join-Path $ResourcesPath "appicon.icns")
} else {
    Write-Host "Warning: App icon not found at $IconSource"
}

# Moving all published files to MacOS directory
Write-Host "Moving application files..."
Get-ChildItem -Path $OutputDir -Exclude $AppName | ForEach-Object {
    Move-Item -Path $_.FullName -Destination $MacOSPath -Force
}

# Make the main executable file executable
$ExecutablePath = Join-Path $MacOSPath "ControlR.DesktopClient"
if (Test-Path $ExecutablePath) {
    Write-Host "Setting executable permissions on $ExecutablePath"
    chmod +x $ExecutablePath
} else {
    Write-Host "Warning: Main executable not found at $ExecutablePath"
    # List files to help debug
    Write-Host "Files in MacOS directory:"
    Get-ChildItem -Path $MacOSPath | ForEach-Object { Write-Host "  $($_.Name)" }
}

Write-Host "Mac app bundle created successfully at: $AppPath"