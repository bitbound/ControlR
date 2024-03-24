[System.IO.Directory]::CreateDirectory("..\ControlR.Server\wwwroot\downloads\");
[System.IO.Directory]::CreateDirectory(".\out\make\zip");

Get-ChildItem -Path ".\out\make\zip" | Remove-Item -Force -Recurse
electron-forge make --targets @electron-forge/maker-zip
Copy-Item ".\out\make\zip\win32\x64\controlr-streamer-win*.zip" -Destination "..\ControlR.Server\wwwroot\Downloads\controlr-streamer-win.zip" -Force

#electron-forge make --targets @electron-forge/maker-zip --platform linux
#Copy-Item ".\out\make\zip\linux\x64\controlr-streamer-linux*.zip" -Destination "..\ControlR.Server\wwwroot\Downloads\controlr-streamer-linux.zip" -Force