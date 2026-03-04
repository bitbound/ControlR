# ControlR Avalonia Viewer Example

This example demonstrates how to use the ControlR Avalonia Viewer in a local dev environment.

## Configuration

For local dev, use `User Secrets` to supply the configuration. To set up:

### Using .NET User Secrets (Recommended)

```bash
cd Examples/ControlR.AvaloniaViewerExample

# Set your server URL
dotnet user-secrets set "ViewerOptions:BaseUrl" "https://localhost:7033"

# Set the device ID you want to connect to
dotnet user-secrets set "ViewerOptions:DeviceId" "f9ae6af7-e397-4a68-8b78-492f822dd7eb"

# Set your Personal Access Token
dotnet user-secrets set "ViewerOptions:PersonalAccessToken" "your-pat-here"
```

### Using appsettings.json
Alternatively, create an `appsettings.json` file (gitignored):

```json
{
  "ControlrViewerOptions": {
    "BaseUrl": "https://localhost:7033",
    "DeviceId": "f9ae6af7-e397-4a68-8b78-492f822dd7eb",
    "PersonalAccessToken": "your-pat-here"
  }
}
```

**WARNING**: Never commit `appsettings.json` with real credentials.

### Injecting Options into ControlrViewer

See [App.axaml.cs](./App.axaml.cs), [MainWindow.axaml](./Views/MainWindow.axaml), and [MainWindowViewModel.cs](./ViewModels/MainWindowViewModel.cs) for an example of how to realize the options and pass them into the `ControlrViewer` control via data binding.

## Running the Application

Once configured, simply run:

```bash
dotnet run
```
