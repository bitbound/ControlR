param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Experimental", "Preview", "Prod")]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [string]$OctodiffPath
)

$ArtifactsDir = Join-Path -Path $env:ArtifactsShare -ChildPath ($Environment.ToLower())
$WorkDir = $env:SYSTEM_DEFAULTWORKINGDIRECTORY

[System.IO.Directory]::CreateDirectory("$ArtifactsDir\Server\wwwroot\downloads\deltas") | Out-Null
[System.IO.Directory]::CreateDirectory("$ArtifactsDir\Server\wwwroot\downloads\signatures") | Out-Null
Copy-Item -Path "$ArtifactsDir\Server\wwwroot\downloads\deltas\" -Destination "$WorkDir\_ControlR\Server\wwwroot\downloads\deltas\" -Recurse -Force
Copy-Item -Path "$ArtifactsDir\Server\wwwroot\downloads\signatures\" -Destination "$WorkDir\_ControlR\Server\wwwroot\downloads\signatures\" -Recurse -Force

&"$WorkDir\_ControlR\BuildScripts\CreateDelta.ps1" -DownloadsFolder "$WorkDir/_ControlR/Server/wwwroot/downloads" -OldFilePath "$ArtifactsDir\Server\wwwroot\downloads\win-x86\ControlR.Streamer.zip" -NewFilePath "$WorkDir\_ControlR\Server\wwwroot\downloads\win-x86\ControlR.Streamer.zip" -BaseDeltaName "windows-streamer" -OctodiffPath $OctodiffPath

Compress-Archive -Path "$WorkDir\_ControlR\Server" -DestinationPath "$ArtifactsDir\Server-Linux-x64.zip" -Force

Copy-Item -Path "$WorKDir\_ControlR\*" -Destination "$ArtifactsDir" -Recurse -Force