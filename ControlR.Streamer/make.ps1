function Check-LastExitCode {
  if ($LASTEXITCODE -and $LASTEXITCODE -gt 0) {
    throw "Received exit code $LASTEXITCODE.  Aborting."
  }
}

[System.IO.Directory]::CreateDirectory("..\ControlR.Server\wwwroot\downloads\") | Out-Null;
[System.IO.Directory]::CreateDirectory(".\out\make\zip") | Out-Null;

Get-ChildItem -Path ".\out\make\zip" | Remove-Item -Force -Recurse
electron-forge make --targets @electron-forge/maker-zip
Check-LastExitCode

Copy-Item ".\out\make\zip\win32\x64\controlr-streamer-win*.zip" -Destination "..\ControlR.Server\wwwroot\Downloads\controlr-streamer-win.zip" -Force
Check-LastExitCode

#electron-forge make --targets @electron-forge/maker-zip --platform linux
#Copy-Item ".\out\make\zip\linux\x64\controlr-streamer-linux*.zip" -Destination "..\ControlR.Server\wwwroot\Downloads\controlr-streamer-linux.zip" -Force