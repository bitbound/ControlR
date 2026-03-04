# ControlR.Viewer.Avalonia

`ControlR.Viewer.Avalonia` provides the `ControlrViewer` control for embedding a ControlR remote viewer inside an Avalonia UI application.

Use it when you want to host a live ControlR session inside one of your existing views (not necessarily your main window).

## Install

```bash
dotnet add package ControlR.Viewer.Avalonia
```

## Requirements

Your view model must expose a `ControlrViewerOptions` instance containing:

- `BaseUrl` (`Uri`) - Base URL of your ControlR server.
- `DeviceId` (`Guid`) - Device to connect to.
- `PersonalAccessToken` (`string`) - PAT for the connecting user.

How those values are resolved is up to your application architecture.

## Usage Example

In this example, `ControlrViewer` is hosted in `ParentView.axaml`, with options provided by `ParentViewModel`.

### ParentView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:YourApp.ViewModels"
             xmlns:ctrlr="using:ControlR.Viewer.Avalonia"
             x:Class="YourApp.Views.ParentView"
             x:DataType="vm:ParentViewModel">

	<ctrlr:ControlrViewer Options="{Binding ViewerOptions}" />
</UserControl>
```

### ParentViewModel.cs

```csharp
using ControlR.Libraries.Viewer.Common.Options;
using Microsoft.Extensions.Options;

namespace YourApp.ViewModels;

public class ParentViewModel
{
  public ParentViewModel(IOptions<ControlrViewerOptions> viewerOptions)
  {
    ViewerOptions = viewerOptions.Value;
  }

  public ControlrViewerOptions ViewerOptions { get; }
}
```

## ViewerRegistry

The library exposes a global `ViewerRegistry` helper (in `ControlR.Viewer.Avalonia.Services`) that stores information about the `ControlrViewer` and `IServiceProvider` instances that are currently active. This allows you to interact with the viewers and their services from anywhere in your application, as long as you have the viewer's instance ID (which is a `Guid`).

Instances are automatically registered when a `ControlrViewer` is initialized and unregistered/disposed when the `ControlrViewer` instance is detached from the visual tree, so you don't need to manage the lifecycle manually.

Important APIs:

- `ViewerRegistry.Register(Guid instanceId, ControlrViewer viewer, IServiceProvider serviceProvider)` — registers a viewer instance (the control registers itself automatically).
- `ViewerRegistry.Unregister(Guid instanceId)` — removes a registered instance (the control unregisters itself on disposal).
- `ViewerRegistry.GetRequiredService<T>(Guid instanceId)` — resolves a required service from a specific viewer's scope and throws if not found.
- `ViewerRegistry.GetService<T>(Guid instanceId)` — attempts to resolve a service and returns null if not found.
- `ViewerRegistry.GetService(Guid instanceId, Type serviceType)` — non-generic service resolution.
- `ViewerRegistry.GetAllInstanceIds()` — returns all currently registered viewer instance IDs.
- `ViewerRegistry.TryGetInstance(Guid instanceId, out ControlrViewer? viewer)` — attempts to get a registered viewer instance.

Example usage:

```csharp
// Obtain a service from a running viewer (registration is handled by the control itself)
var remoteControlStream = ViewerRegistry.GetRequiredService<IViewerRemoteControlStream>(viewerId);
```

## Notes

- Keep PATs out of source control.
- The `ControlrViewer` will intitialize and connect to the server once it's made visible.
- Options will be validated automatically before connecting.
  - If any options are missing or invalid, the control will render an error message instead.

