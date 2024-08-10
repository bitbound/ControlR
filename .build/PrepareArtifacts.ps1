param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Experimental", "Preview", "Prod")]
    [string]$Environment
)


$ArtifactsDir = Join-Path -Path $env:ArtifactsShare -ChildPath ($Environment.ToLower())
$WorkDir = $env:SYSTEM_DEFAULTWORKINGDIRECTORY

function Create-ReleaseFile($FilePath) {
    $Hash = (Get-FileHash -Path $FilePath -Algorithm MD5).Hash
    New-Item -Path "$ArtifactsDir\Releases" -Name $Hash -ItemType File -Force | Out-Null
}

New-Item -Path "$ArtifactsDir" -Name "Releases" -ItemType Directory -Force | Out-Null

Compress-Archive -Path "$WorkDir\_ControlR\Server" -DestinationPath "$ArtifactsDir\Server-Linux-x64.zip" -Force

Create-ReleaseFile -FilePath "$WorkDir\_ControlR\Server\wwwroot\downloads\win-x86\ControlR.Agent.exe"
Create-ReleaseFile -FilePath "$WorkDir\_ControlR\Server\wwwroot\downloads\win-x86\ControlR.Streamer.zip"
Create-ReleaseFile -FilePath "$WorkDir\_ControlR\Server\wwwroot\downloads\linux-x64\ControlR.Agent"

Copy-Item -Path "$WorKDir\_ControlR\*" -Destination "$ArtifactsDir" -Recurse -Force