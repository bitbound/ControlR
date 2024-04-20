param (
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePassword,

    [Parameter(Mandatory = $true)]
    [string]$SignToolPath,

    [Parameter(Mandatory = $true)]
    [string]$KeystorePassword,

    [Parameter(Mandatory = $true)]
    [string]$CurrentVersion,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [switch]$BuildAgent,

    [switch]$BuildViewer,

    [switch]$BuildStreamer,

    [switch]$BuildWebsite
)


$InstallerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
$VsWhere = "$InstallerDir\vswhere.exe"
$MSBuildPath = (&"$VsWhere" -latest -products * -find "\MSBuild\Current\Bin\MSBuild.exe").Trim()
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$DownloadsFolder = "$Root\ControlR.Server\wwwroot\downloads"
$PublishedDownloads = "$OutputPath\wwwroot\downloads"
$Octodiff = "$Root\.build\octodiff.exe"

function Check-LastExitCode {
    if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
        throw "Received exit code $LASTEXITCODE.  Aborting."
    }
}

function Create-Signature($FilePath, $BaseSignatureFileName) {
    if (!(Test-Path -Path $FilePath)) {
        return "";
    }

    [System.IO.Directory]::CreateDirectory("$PublishedDownloads\signatures") | Out-Null
    $Hash = Get-FileHash -Algorithm MD5 -Path $FilePath | Select-Object -ExpandProperty Hash
    $SignaturePath = "$PublishedDownloads\signatures\$BaseSignatureFileName-$Hash.octosig"
    &"$Octodiff" signature $FilePath $SignaturePath
    Check-LastExitCode
    return $SignaturePath
}

function Create-Delta($SignaturePath, $NewFilePath) {
    if (!(Test-Path -Path $SignaturePath)) {
        return;
    }

    [System.IO.Directory]::CreateDirectory("$PublishedDownloads\deltas") | Out-Null
    $DeltaFileName = (Get-Item -Path $SignaturePath).Name.Replace(".octosig", ".octodelta")
    &"$Octodiff" delta $SignaturePath $NewFilePath "$PublishedDownloads\deltas\$DeltaFileName"
    Check-LastExitCode
}


if (!$CurrentVersion) {
    Write-Error "CurrentVersion is required."
}

if (!(Test-Path $CertificatePath)) {
    Write-Error "Certificate not found."
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
New-Item -Path "$PublishedDownloads" -ItemType Directory -Force | Out-Null

#$WinSigPath = Create-Signature -FilePath "$PublishedDownloads\win-x86\ControlR.Agent.exe" -BaseSignatureFileName "windows-agent"
#$LinuxSigPath = Create-Signature -FilePath "$PublishedDownloads\linux-x64\ControlR.Agent" -BaseSignatureFileName "linux-agent"
$WinSigPath = Create-Signature -FilePath "$PublishedDownloads\controlr-streamer-win.zip" -BaseSignatureFileName "windows-streamer"

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

    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.msix" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.msix" -Force
    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "ControlR*.cer" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.cer" -Force

    Remove-Item -Path "$Root\ControlR.Viewer\bin\publish\" -Force -Recurse -ErrorAction SilentlyContinue
    dotnet publish "$Root\ControlR.Viewer\" -f:net8.0-android -c:Release /p:AndroidSigningKeyPass=$KeystorePassword /p:AndroidSigningStorePass=$KeystorePassword -o "$Root\ControlR.Viewer\bin\publish\"
    Check-LastExitCode

    Get-ChildItem -Path "$Root\ControlR.Viewer\bin\publish\" -Recurse -Include "*Signed.apk" | Select-Object -First 1 | Copy-Item -Destination "$DownloadsFolder\ControlR.Viewer.apk" -Force

    Set-Content -Path "$DownloadsFolder\ViewerVersion.txt" -Value $CurrentVersion.ToString() -Force -Encoding UTF8
}


if ($BuildStreamer) {
    [string]$PackageJson = Get-Content -Path "$Root\ControlR.Streamer\package.json"
    $Package = $PackageJson | ConvertFrom-Json
    $Package.version = $CurrentVersion.Split(".", [System.StringSplitOptions]::RemoveEmptyEntries) | Select-Object -First 3 | Join-String -Separator "."
    [string]$PackageJson = $Package | ConvertTo-Json
    [System.IO.File]::WriteAllText("$Root\ControlR.Streamer\package.json", $PackageJson)
    Push-Location "$Root\ControlR.Streamer"
    npm install
    npm run make-pwsh
    Pop-Location
    
}

dotnet publish -p:ExcludeApp_Data=true --runtime linux-x64 --configuration Release --output $OutputPath --self-contained true "$Root\ControlR.Server\"

#Create-Delta -SignaturePath $WinSigPath -NewFilePath "$PublishedDownloads\win-x86\ControlR.Agent.exe"
#Create-Delta -SignaturePath $LinuxSigPath -NewFilePath "$PublishedDownloads\linux-x64\ControlR.Agent"
Create-Delta -SignaturePath $WinSigPath -NewFilePath "$PublishedDownloads\controlr-streamer-win.zip"

if ($BuildWebsite) {
    [System.IO.Directory]::CreateDirectory("$Root\ControlR.Website\public\downloads\")
    Get-ChildItem -Path $PublishedDownloads | Copy-Item -Destination "$Root\ControlR.Website\public\downloads\" -Recurse -Force
    Push-Location "$Root\ControlR.Website"
    npm install
    npm run build
    Pop-Location
    Check-LastExitCode
}