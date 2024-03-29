param (
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,

    [Parameter(Mandatory=$true)]
    [string]$CertificatePassword,

    [Parameter(Mandatory=$true)]
    [string]$SignToolPath,

    [Parameter(Mandatory=$true)]
    [string]$KeystorePassword,

    [Parameter(Mandatory=$true)]
    [string]$TightVncResourcesDir,

    [string]$CurrentVersion,

    [switch]$BuildAgent,

    [switch]$BuildViewer,

    [switch]$BuildWebsite
)

function Check-LastExitCode {
    if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
        throw "Received exit code $LASTEXITCODE.  Aborting."
    }
}

$InstallerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
$VsWhere = "$InstallerDir\vswhere.exe"
$MSBuildPath = (&"$VsWhere" -latest -products * -find "\MSBuild\Current\Bin\MSBuild.exe").Trim()
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$DownloadsFolder = "$Root\ControlR.Server\wwwroot\downloads"

if (!$CurrentVersion) {
    Push-Location -Path $Root

    $VersionString = git show -s --format=%ci
    $VersionDate = [DateTimeOffset]::Parse($VersionString)

    $CurrentVersion = $VersionDate.ToString("yyyy.M.d.Hmm")

    Pop-Location
}

if (!(Test-Path $CertificatePath)) {
    Write-Error "Certificate not found."
    return
}

if (!(Test-Path $SignToolPath)) {
    Write-Error "SignTool not found."
    return
}

if (!(Test-Path $TightVncResourcesDir)) {
    Write-Error "TightVncResources directory not found."
    return
}

if (!(Test-Path "$TightVncResourcesDir\Server")) {
    Write-Error "Expected a folder named 'Server' in the TightVNC resources directory."
    return
}

if (!(Test-Path "$TightVncResourcesDir\Viewer")) {
    Write-Error "Expected a folder named 'Server' in the TightVNC resources directory."
    return
}

Set-Location $Root

if (!(Test-Path -Path "$Root\ControlR.sln")) {
    Write-Host "Unable to determine solution directory." -ForegroundColor Red
    return
}

[System.IO.Directory]::CreateDirectory("$Root\ControlR.Agent\Resources\TightVnc")
[System.IO.Directory]::CreateDirectory("$Root\ControlR.Viewer\VncResources")
Get-ChildItem -Path "$TightVncResourcesDir\Server" | Copy-Item -Destination "$Root\ControlR.Agent\Resources\TightVnc" -Force
Get-ChildItem -Path "$TightVncResourcesDir\Viewer" | Copy-Item -Destination "$Root\ControlR.Viewer\VncResources" -Force

if ($BuildAgent){
    dotnet publish --configuration Release -p:PublishProfile=win-x86 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode

    dotnet publish --configuration Release -p:PublishProfile=osx-arm64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode

    dotnet publish --configuration Release -p:PublishProfile=osx-x64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode

    dotnet publish --configuration Release -p:PublishProfile=linux-x64 -p:Version=$CurrentVersion -p:FileVersion=$CurrentVersion -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:IncludeAppSettingsInSingleFile=true  "$Root\ControlR.Agent\"
    Check-LastExitCode

    Start-Sleep -Seconds 1
    &"$SignToolPath" sign /fd SHA256 /f "$CertificatePath" /p $CertificatePassword /t http://timestamp.digicert.com "$DownloadsFolder\win-x86\ControlR.Agent.exe"
    Check-LastExitCode

    Set-Content -Path "$DownloadsFolder\AgentVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}


if ($BuildViewer) {
    $Csproj = Select-Xml -XPath "/" -Path "$Root\ControlR.Viewer\ControlR.Viewer.csproj"
    $AppVersion = $Csproj.Node.SelectNodes("//ApplicationVersion")
    $AppVersion[0].InnerText = "1";

    $DisplayVersion = $Csproj.Node.SelectNodes("//ApplicationDisplayVersion")
    $DisplayVersion[0].InnerText = $CurrentVersion.ToString()
    Set-Content -Path  "$Root\ControlR.Viewer\ControlR.Viewer.csproj" -Value $Csproj.Node.OuterXml.Trim()

    $Manifest = Select-Xml -XPath "/" -Path "$Root\ControlR.Viewer\Platforms\Windows\Package.appxmanifest"
    #$Version = [System.Version]::Parse($Manifest.Node.Package.Identity.Version)
    #$NewVersion = [System.Version]::new($Version.Major, $Version.Minor, $Version.Build + 1, $Version.Revision)
    $Manifest.Node.Package.Identity.Version = $CurrentVersion.ToString()
    Set-Content -Path "$Root\ControlR.Viewer\Platforms\Windows\Package.appxmanifest" -Value $Manifest.Node.OuterXml.Trim()
    Remove-Item -Path "$Root\ControlR.Viewer\bin\publish\" -Force -Recurse -ErrorAction SilentlyContinue
    dotnet publish -p:PublishProfile=msix --configuration Release --framework net8.0-windows10.0.19041.0 "$Root\ControlR.Viewer\"
    Check-LastExitCode

    New-Item -Path "$DownloadsFolder" -ItemType Directory -Force
    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.msix" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.msix" -Force
    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.cer" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.cer" -Force

    Remove-Item -Path "$Root\ControlR.Viewer\bin\publish\" -Force -Recurse -ErrorAction SilentlyContinue
    dotnet publish "$Root\ControlR.Viewer\" -f:net8.0-android -c:Release /p:AndroidSigningKeyPass=$KeystorePassword /p:AndroidSigningStorePass=$KeystorePassword -o "$Root\ControlR.Viewer\bin\publish\"
    Check-LastExitCode

    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "*Signed.apk" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.apk" -Force

    Set-Content -Path "$DownloadsFolder\ViewerVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}


if ($BuildWebsite) {
    [System.IO.Directory]::CreateDirectory("$Root\ControlR.Website\public\downloads\")
    Get-ChildItem -Path "$Root\ControlR.Server\wwwroot\downloads\" | Copy-Item -Destination "$Root\ControlR.Website\public\downloads\" -Recurse -Force
    Push-Location "$Root\ControlR.Website"
    npm install
    npm run build
    Pop-Location
    Check-LastExitCode
}

dotnet publish -p:ExcludeApp_Data=true --runtime linux-x64 --configuration Release --output "$Root\ControlR.Server\bin\publish" --self-contained true "$Root\ControlR.Server\"