param (
  [Parameter(Mandatory = $true)]
  [string]$CertificateThumbprint,

  [Parameter(Mandatory = $true)]
  [string]$SignToolPath,

  [Parameter(Mandatory = $true)]
  [string]$CurrentVersion,

  [Parameter(Mandatory = $true)]
  [string]$OutputPath,

  [string]$Configuration = "Release",

  [switch]$BuildAgent,

  [switch]$BuildDesktop
)

#$InstallerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
#$VsWhere = "$InstallerDir\vswhere.exe"
#$MSBuildPath = (&"$VsWhere" -latest -prerelease -products * -find "\MSBuild\Current\Bin\MSBuild.exe").Trim()
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$DownloadsFolder = "$Root\ControlR.Web.Server\wwwroot\downloads"
$StagingFolder = "$Root\.build\staging-desktop"

function Check-LastExitCode {
  if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
    throw "Received exit code $LASTEXITCODE.  Aborting."
  }
}

function Wait-ForFileToExist([string]$FilePath) {
  while (!(Test-Path -Path $FilePath)) {
    Start-Sleep -Seconds 1
  }
}

if (!$CurrentVersion) {
  Write-Error "CurrentVersion is required."
}

if (!$CertificateThumbprint) {
  Write-Error "CertificateThumbprint cannot be empty."
  return
}

if (!(Test-Path $SignToolPath)) {
  Write-Error "SignTool not found."
  return
}

Set-Location $Root

if (!(Test-Path -Path "$Root\ControlR.sln")) {
  Write-Host "Unable to determine solution directory." -ForegroundColor Red
  return
}

New-Item -Path "$DownloadsFolder" -ItemType Directory -Force | Out-Null
New-Item -Path "$StagingFolder" -ItemType Directory -Force | Out-Null

$AgentResourcesFolder = "$Root\ControlR.Agent.Common\Resources"
New-Item -Path "$AgentResourcesFolder" -ItemType Directory -Force | Out-Null

$DesktopPublishPath = "$Root\ControlR.DesktopClient\bin\publish\"
$DesktopCommonArgs = @(
  "-c", $Configuration,
  "--self-contained",
  "-p:Version=$CurrentVersion",
  "-p:FileVersion=$CurrentVersion"
)

$AgentCommonArgs = @(
  "-c", $Configuration,
  "-p:Version=$CurrentVersion",
  "-p:FileVersion=$CurrentVersion",
  "-p:PublishSingleFile=true",
  "-p:IncludeAllContentForSelfExtract=true",
  "-p:EnableCompressionInSingleFile=true",
  "-p:IncludeAppSettingsInSingleFile=true"
)

function Build-DesktopAndAgent {
  param(
    [string]$RuntimeId,
    [string]$DesktopExeName,
    [string]$AgentExeName,
    [string]$ZipFileName,
    [bool]$SignExecutables
  )
  
  Write-Host "`n========================================" -ForegroundColor Cyan
  Write-Host "Building for $RuntimeId" -ForegroundColor Cyan
  Write-Host "========================================" -ForegroundColor Cyan
  
  # Clean Resources folder of Desktop ZIPs
  Write-Host "Cleaning Resources folder..." -ForegroundColor Yellow
  Get-ChildItem -Path $AgentResourcesFolder -Filter "*.zip" | Remove-Item -Force
  
  # Build Desktop Client
  if ($BuildDesktop) {
    Write-Host "Publishing DesktopClient for $RuntimeId..." -ForegroundColor Green
    dotnet publish -r $RuntimeId -o "$DesktopPublishPath\$RuntimeId" $DesktopCommonArgs "$Root\ControlR.DesktopClient\"
    Check-LastExitCode
    
    if ($SignExecutables) {
      Wait-ForFileToExist -FilePath "$DesktopPublishPath\$RuntimeId\$DesktopExeName"
      Write-Host "Signing $DesktopExeName..." -ForegroundColor Green
      &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$DesktopPublishPath\$RuntimeId\$DesktopExeName"
      Check-LastExitCode
    }
    
  Write-Host "Creating DesktopClient ZIP (staged, not copied to server)..." -ForegroundColor Green
  New-Item -Path "$StagingFolder\$RuntimeId" -ItemType Directory -Force | Out-Null
  Compress-Archive -Path "$DesktopPublishPath\$RuntimeId\*" -DestinationPath "$StagingFolder\$RuntimeId\$ZipFileName" -Force
    
    # Copy to Resources folder for embedding
  Write-Host "Copying $ZipFileName to Agent Resources folder..." -ForegroundColor Green
  Copy-Item "$StagingFolder\$RuntimeId\$ZipFileName" "$AgentResourcesFolder\$ZipFileName" -Force
  }
  
  # Build Agent
  if ($BuildAgent) {
    Write-Host "Publishing Agent for $RuntimeId..." -ForegroundColor Green
    dotnet publish -r $RuntimeId -o "$DownloadsFolder\$RuntimeId\" $AgentCommonArgs "$Root\ControlR.Agent\"
    Check-LastExitCode
    
    if ($SignExecutables) {
      Wait-ForFileToExist -FilePath "$DownloadsFolder\$RuntimeId\$AgentExeName"
      Write-Host "Signing $AgentExeName..." -ForegroundColor Green
      &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$DownloadsFolder\$RuntimeId\$AgentExeName"
      Check-LastExitCode
    }
  }
  
  Write-Host "Completed build for $RuntimeId" -ForegroundColor Cyan
}

# Build for each platform
if ($BuildDesktop -or $BuildAgent) {
  Build-DesktopAndAgent -RuntimeId "win-x86" -DesktopExeName "ControlR.DesktopClient.exe" -AgentExeName "ControlR.Agent.exe" -ZipFileName "ControlR.DesktopClient.zip" -SignExecutables $true
  Build-DesktopAndAgent -RuntimeId "win-x64" -DesktopExeName "ControlR.DesktopClient.exe" -AgentExeName "ControlR.Agent.exe" -ZipFileName "ControlR.DesktopClient.zip" -SignExecutables $true
  Build-DesktopAndAgent -RuntimeId "linux-x64" -DesktopExeName "ControlR.DesktopClient" -AgentExeName "ControlR.Agent" -ZipFileName "ControlR.DesktopClient.zip" -SignExecutables $false
  
  # Mac builds would be done on macOS with code signing
  # Build-DesktopAndAgent -RuntimeId "osx-x64" -DesktopExeName "ControlR.DesktopClient" -AgentExeName "ControlR.Agent" -ZipFileName "ControlR.app.zip" -SignExecutables $false
  # Build-DesktopAndAgent -RuntimeId "osx-arm64" -DesktopExeName "ControlR.DesktopClient" -AgentExeName "ControlR.Agent" -ZipFileName "ControlR.app.zip" -SignExecutables $false
  
  if ($BuildAgent) {
    Set-Content -Path "$DownloadsFolder\AgentVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
  }
}

dotnet publish -p:ExcludeApp_Data=true --runtime linux-x64 --configuration $Configuration -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion --output $OutputPath --self-contained true "$Root\ControlR.Web.Server\"