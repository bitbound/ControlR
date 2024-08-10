param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Experimental", "Preview", "Prod")]
    [string]$Environment
)

$ArtifactsDir = Join-Path -Path $env:ArtifactsShare -ChildPath ($Environment.ToLower())
$WorkDir = $env:SYSTEM_DEFAULTWORKINGDIRECTORY

Compress-Archive -Path "$WorkDir\_ControlR\Server" -DestinationPath "$ArtifactsDir\Server-Linux-x64.zip" -Force

Copy-Item -Path "$WorKDir\_ControlR\*" -Destination "$ArtifactsDir" -Recurse -Force