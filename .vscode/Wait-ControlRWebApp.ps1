param(
  [Parameter(Mandatory = $true)]
  [string]$Url,

  [int]$TimeoutSeconds = 90,

  [int]$IntervalMilliseconds = 1000
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

while ((Get-Date) -lt $deadline) {
  try {
    $response = Invoke-WebRequest -Uri $Url -SkipCertificateCheck -MaximumRedirection 5
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
      Write-Host "Web app is ready at $Url"
      exit 0
    }
  }
  catch {
  }

  Start-Sleep -Milliseconds $IntervalMilliseconds
}

Write-Error "Timed out waiting for $Url"
exit 1