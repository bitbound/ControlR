param (
  [Parameter(Mandatory = $true)]
  [string]$DownloadsFolder,
  [Parameter(Mandatory = $true)]
  [string]$OldFilePath,
  [Parameter(Mandatory = $true)]
  [string]$NewFilePath,
  [Parameter(Mandatory = $true)]
  [string]$BaseDeltaName,
  [Parameter(Mandatory = $true)]
  [string]$OctodiffPath
)

function Check-LastExitCode {
  if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
    throw "Received exit code $LASTEXITCODE.  Aborting."
  }
}

function Create-Signature() {
  [System.IO.Directory]::CreateDirectory("$DownloadsFolder\signatures") | Out-Null
  $Hash = Get-FileHash -Algorithm MD5 -Path $OldFilePath | Select-Object -ExpandProperty Hash
  $SignaturePath = "$DownloadsFolder\signatures\$BaseDeltaName-$Hash.octosig"
  &"$OctodiffPath" signature $OldFilePath $SignaturePath
  Check-LastExitCode
  return $SignaturePath
}

function Create-Delta($SignaturePath) {
  [System.IO.Directory]::CreateDirectory("$DownloadsFolder\deltas") | Out-Null
  $DeltaFileName = (Get-Item -Path $SignaturePath).Name.Replace(".octosig", ".octodelta")
  &"$OctodiffPath" delta $SignaturePath $NewFilePath "$DownloadsFolder\deltas\$DeltaFileName"
  Check-LastExitCode
}


$WinSigPath = Create-Signature -OldFilePath $OldFilePath
Create-Delta -SignaturePath $WinSigPath
