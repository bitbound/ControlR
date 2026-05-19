# ControlR Avalonia Viewer Example

This example hosts `ControlrViewer` in a standalone Avalonia desktop application and signs in with the same email and password used in the web app.

## Configuration

Supply the server URL and target device ID with user secrets:

```powershell
dotnet user-secrets set "ControlrViewerOptions:BaseUrl" "https://localhost:7033" --project Examples/ControlR.AvaloniaViewerExample/ControlR.AvaloniaViewerExample.csproj
dotnet user-secrets set "ControlrViewerOptions:DeviceId" "f9ae6af7-e397-4a68-8b78-492f822dd7eb" --project Examples/ControlR.AvaloniaViewerExample/ControlR.AvaloniaViewerExample.csproj
```

The sample prompts for the account email and password at runtime. If the account has two-factor authentication enabled, the app asks for the authenticator code after the initial sign-in attempt.

## Run

```powershell
dotnet run --project Examples/ControlR.AvaloniaViewerExample/ControlR.AvaloniaViewerExample.csproj
```

The sample keeps the issued bearer and refresh tokens in memory only and refreshes them automatically while the app remains open.
