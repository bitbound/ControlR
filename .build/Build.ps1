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

  [switch]$BuildStreamer,

  [switch]$UpdateMacVersions
)


$InstallerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
$VsWhere = "$InstallerDir\vswhere.exe"
$MSBuildPath = (&"$VsWhere" -latest -prerelease -products * -find "\MSBuild\Current\Bin\MSBuild.exe").Trim()
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$DownloadsFolder = "$Root\ControlR.Web.Server\wwwroot\downloads"

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

if ($BuildAgent) {
  $CommonArgs = @(
    "-c", $Configuration,
    "-p:Version=$CurrentVersion",
    "-p:FileVersion=$CurrentVersion",
    "-p:PublishSingleFile=true",
    "-p:IncludeAllContentForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeAppSettingsInSingleFile=true"
  )
  
  dotnet publish -r win-x86 -o "$DownloadsFolder\win-x86\" $CommonArgs "$Root\ControlR.Agent\"
  Check-LastExitCode

  dotnet publish -r win-x64 -o "$DownloadsFolder\win-x64\" $CommonArgs "$Root\ControlR.Agent\"
  Check-LastExitCode

  dotnet publish -r linux-x64 -o "$DownloadsFolder\linux-x64\" $CommonArgs "$Root\ControlR.Agent\"
  Check-LastExitCode

  # These will need to be built on MacOS for code-signing.
  #dotnet publish -r osx-arm64 -o "$DownloadsFolder\osx-arm64\" $CommonArgs "$Root\ControlR.Agent\"
  #Check-LastExitCode
  #dotnet publish -r osx-x64 -o "$DownloadsFolder\osx-x64\" $CommonArgs "$Root\ControlR.Agent\"
  #Check-LastExitCode

  Wait-ForFileToExist -FilePath "$DownloadsFolder\win-x86\ControlR.Agent.exe"
  &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$DownloadsFolder\win-x86\ControlR.Agent.exe"
  Check-LastExitCode

  if (!(Test-Path -Path "$DownloadsFolder\AgentVersions.json")){
    Set-Content -Path "$DownloadsFolder\AgentVersions.json" -Value "{}" -Force -Encoding UTF8
  }
  [hashtable]$AgentVersions = Get-Content -Path "$DownloadsFolder\AgentVersions.json" | ConvertFrom-Json
  if (!$AgentVersions) {
    $AgentVersions = @{}
  }

  $AgentVersions["WinX86"] = $CurrentVersion
  $AgentVersions["WinX64"] = $CurrentVersion
  $AgentVersions["LinuxX64"] = $CurrentVersion

  if ($UpdateMacVersions) {
    $AgentVersions["MacOsArm64"] = $CurrentVersion
    $AgentVersions["MacOsX64"] = $CurrentVersion
  }

  Set-Content -Path "$DownloadsFolder\AgentVersions.json" -Value ($AgentVersions | ConvertTo-Json) -Force -Encoding UTF8
  Set-Content -Path "$DownloadsFolder\AgentVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}

if ($BuildStreamer) {
  dotnet publish --configuration $Configuration -p:PublishProfile=win-x86 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion "$Root\ControlR.Streamer\"

  Wait-ForFileToExist -FilePath "$Root\ControlR.Streamer\bin\publish\ControlR.Streamer.exe"
  &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$Root\ControlR.Streamer\bin\publish\ControlR.Streamer.exe"
  Check-LastExitCode

  Compress-Archive -Path "$Root\ControlR.Streamer\bin\publish\*" -DestinationPath "$DownloadsFolder\win-x86\ControlR.Streamer.zip" -Force
}

dotnet publish -p:ExcludeApp_Data=true --runtime linux-x64 --configuration $Configuration -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion --output $OutputPath --self-contained true "$Root\ControlR.Web.Server\"
