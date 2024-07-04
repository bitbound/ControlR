param (
    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint,

    [Parameter(Mandatory = $true)]
    [string]$SignToolPath,

    [Parameter(Mandatory = $true)]
    [string]$KeystorePath,

    [Parameter(Mandatory = $true)]
    [string]$KeystorePassword,

    [Parameter(Mandatory = $true)]
    [string]$CurrentVersion,

    [Parameter(Mandatory = $true)]
    [int]$AndroidVersionCode,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [switch]$BuildAgent,

    [switch]$BuildViewer,

    [switch]$BuildStreamer,

    [switch]$BuildWebsite
)


$InstallerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
$VsWhere = "$InstallerDir\vswhere.exe"
$MSBuildPath = (&"$VsWhere" -latest -prerelease -products * -find "\MSBuild\Current\Bin\MSBuild.exe").Trim()
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$DownloadsFolder = "$Root\ControlR.Server\wwwroot\downloads"

function Check-LastExitCode {
    if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
        throw "Received exit code $LASTEXITCODE.  Aborting."
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

    dotnet publish --configuration Release -p:PublishProfile=win-x86 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode
    
    dotnet publish --configuration Release -p:PublishProfile=linux-x64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode

    #dotnet publish --configuration Release -p:PublishProfile=osx-arm64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    #Check-LastExitCode

    #dotnet publish --configuration Release -p:PublishProfile=osx-x64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    #Check-LastExitCode

    Start-Sleep -Seconds 1
    &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$DownloadsFolder\win-x86\ControlR.Agent.exe"
    Check-LastExitCode

    Set-Content -Path "$DownloadsFolder\AgentVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}


if ($BuildViewer) {
    $Csproj = Select-Xml -XPath "/" -Path "$Root\ControlR.Viewer\ControlR.Viewer.csproj"
    $AppVersion = $Csproj.Node.SelectNodes("//ApplicationVersion")
    $AppVersion[0].InnerText = "$AndroidVersionCode";

    $DisplayVersion = $Csproj.Node.SelectNodes("//ApplicationDisplayVersion")
    $DisplayVersion[0].InnerText = $CurrentVersion.ToString()
    Set-Content -Path  "$Root\ControlR.Viewer\ControlR.Viewer.csproj" -Value $Csproj.Node.OuterXml.Trim()

    $Manifest = Select-Xml -XPath "/" -Path "$Root\ControlR.Viewer\Platforms\Windows\Package.appxmanifest"
    #$Version = [System.Version]::Parse($Manifest.Node.Package.Identity.Version)
    #$NewVersion = [System.Version]::new($Version.Major, $Version.Minor, $Version.Build + 1, $Version.Revision)
    $Manifest.Node.Package.Identity.Version = $CurrentVersion.ToString()
    Set-Content -Path "$Root\ControlR.Viewer\Platforms\Windows\Package.appxmanifest" -Value $Manifest.Node.OuterXml.Trim()
    Remove-Item -Path "$Root\ControlR.Viewer\bin\publish\" -Force -Recurse -ErrorAction SilentlyContinue
    dotnet publish -p:PublishProfile=sideload --configuration Release --framework net8.0-windows10.0.19041.0 "$Root\ControlR.Viewer\"
    Check-LastExitCode

    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.msix" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.msix" -Force
    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.cer" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.cer" -Force

    Remove-Item -Path "$Root\ControlR.Viewer\bin\publish\" -Force -Recurse -ErrorAction SilentlyContinue
    dotnet publish "$Root\ControlR.Viewer\" -f:net8.0-android -c:Release /p:AndroidKeyStore=True /p:AndroidSigningKeyAlias=controlr /p:AndroidSigningKeyStore="$KeystorePath" /p:AndroidSigningKeyPass=$KeystorePassword /p:AndroidSigningStorePass=$KeystorePassword /p:PackageCertificateThumbprint="$CertificateThumbprint" -o "$Root\ControlR.Viewer\bin\publish\"
    Check-LastExitCode

    Copy-Item -Path "$Root\ControlR.Viewer\bin\publish\dev.jaredg.controlr.viewer-Signed.apk" -Destination "$DownloadsFolder\ControlR.Viewer.apk" -Force
    Copy-Item -Path "$Root\ControlR.Viewer\bin\publish\dev.jaredg.controlr.viewer-Signed.aab" -Destination "$DownloadsFolder\ControlR.Viewer.aab" -Force

    Set-Content -Path "$DownloadsFolder\ViewerVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}


if ($BuildStreamer) {
    dotnet publish --configuration Release -p:PublishProfile=win-x86 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion "$Root\ControlR.Streamer\"
    &"$SignToolPath" sign /fd SHA256 /sha1 "$CertificateThumbprint" /t http://timestamp.digicert.com "$Root\ControlR.Streamer\bin\publish\ControlR.Streamer.exe"
    Check-LastExitCode

    Compress-Archive -Path "$Root\ControlR.Streamer\bin\publish\*" -DestinationPath "$DownloadsFolder\win-x86\ControlR.Streamer.zip" -Force
}

dotnet publish -p:ExcludeApp_Data=true --runtime linux-x64 --configuration Release --output $OutputPath --self-contained true "$Root\ControlR.Server\"


if ($BuildWebsite) {
    [System.IO.Directory]::CreateDirectory("$Root\ControlR.Website\public\downloads\")
    Get-ChildItem -Path "$OutputPath\wwwroot\downloads" | Copy-Item -Destination "$Root\ControlR.Website\public\downloads\" -Recurse -Force
    Push-Location "$Root\ControlR.Website"
    npm install
    npm run build
    Pop-Location
    Check-LastExitCode
}