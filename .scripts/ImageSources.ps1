# Image source staging for customize.ps1.
#
# Asset download URLs from the subscriber portal carry no file extension, only a signed token query
# string, so the source name says nothing about the format. A name can also lie for local sources.
# These helpers stage a source under an extension that matches its bytes, which is what the image
# tools downstream read to pick a decoder.
Set-StrictMode -Version Latest

function Get-ImageKind {
  param([string] $Path)

  $stream = [System.IO.File]::OpenRead($Path)
  try {
    $count = [int][Math]::Min($stream.Length, 4096)
    $head = New-Object byte[] $count
    [void] $stream.Read($head, 0, $count)
  }
  finally {
    $stream.Dispose()
  }

  if ($count -ge 4 -and $head[0] -eq 0x89 -and $head[1] -eq 0x50 -and $head[2] -eq 0x4E -and $head[3] -eq 0x47) {
    return "png"
  }

  # ICO and CUR share this header and differ only in the type field at offset 2.
  if ($count -ge 4 -and $head[0] -eq 0x00 -and $head[1] -eq 0x00 -and ($head[2] -eq 1 -or $head[2] -eq 2) -and $head[3] -eq 0) {
    return "ico"
  }

  # SVG is XML text, so an optional BOM and whitespace may precede the root element. The byte checks
  # above already claimed anything binary, so a root element here is the payload's real format.
  $text = [System.Text.Encoding]::UTF8.GetString($head, 0, $count)
  if ($text -imatch '<svg[\s/>]') {
    return "svg"
  }

  throw "Unrecognized image format in '$Path'. Expected PNG, ICO, or SVG."
}

function Get-SourceFile {
  param([string] $Source, [string] $StagingDir, [string] $BaseName)

  if (-not $Source) { return "" }

  $staged = Join-Path $StagingDir "$BaseName.pending"

  if ($Source -match "^https?://") {
    Write-Host "Downloading $Source"
    Invoke-WebRequest -Uri $Source -OutFile $staged -UseBasicParsing
  }
  elseif ($Source -match "^\\\\") {
    Write-Host "Copying (UNC) $Source"
    Copy-Item -LiteralPath $Source -Destination $staged -Force
  }
  elseif (Test-Path -LiteralPath $Source) {
    Write-Host "Copying $Source"
    Copy-Item -LiteralPath $Source -Destination $staged -Force
  }
  else {
    throw "Source not found: $Source"
  }

  $kind = Get-ImageKind -Path $staged
  $dest = Join-Path $StagingDir "$BaseName.$kind"
  Move-Item -LiteralPath $staged -Destination $dest -Force
  Write-Host "  Staged $kind source -> $dest"
  return $dest
}
