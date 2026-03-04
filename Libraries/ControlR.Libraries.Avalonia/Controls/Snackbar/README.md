# Snackbar (Avalonia)

A lightweight, MudBlazor-style snackbar service for Avalonia.

## What it provides

- `ISnackbar.Add("Message", MessageSeverity.Information)` API
- Host control: `SnackbarHost`
- Per-message dismiss button (`✕`)
- Fade in/out animation
- Configurable defaults via `IOptions<SnackbarOptions>`

Default behavior:

- Visible duration: `2s`
- Fade duration: `0.5s`
- Position: `BottomRight`
- Ordering: new messages appear below older ones

## 1. Register services

```csharp
using ControlR.Libraries.Avalonia.Controls.Snackbar;

services.AddControlrSnackbar();
```

Optional configuration:

```csharp
services.AddControlrSnackbar(options =>
{
  options.VisibleStateDuration = TimeSpan.FromSeconds(3);
  options.FadeDuration = TimeSpan.FromSeconds(0.4);
  options.Position = SnackbarPosition.BottomRight;
  options.NewestOnTop = false;
});
```

## 2. Add host to your top-level parent (e.g. `MainWindow.axaml`)

```axaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:snackbar="using:ControlR.Libraries.Avalonia.Controls.Snackbar"
        x:Class="ControlR.DesktopClient.Views.MainWindow">

  <Grid>
    <!-- Your existing layout/content -->

    <snackbar:SnackbarHost x:Name="SnackbarHost" />
  </Grid>
</Window>
```

## 3. Connect host to DI (`MainWindow.axaml.cs`)

```csharp
using Avalonia.Controls;
using ControlR.Libraries.Avalonia.Controls.Snackbar;

public partial class MainWindow : Window
{
  public MainWindow(IMainWindowViewModel viewModel, ISnackbar snackbar)
  {
    DataContext = viewModel;
    InitializeComponent();

    SnackbarHost.Snackbar = snackbar;
    viewModel.Initialize();
  }
}
```

## 4. Show notifications from view models/services

```csharp
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Shared.Enums;

public class ExampleViewModel
{
  private readonly ISnackbar _snackbar;

  public ExampleViewModel(ISnackbar snackbar)
  {
    _snackbar = snackbar;
  }

  private void Notify()
  {
    _snackbar.Add("Sessions refreshed", MessageSeverity.Information);
  }
}
```

## Notes

- Place only one `SnackbarHost` per top-level window/shell.
- If snackbars do not render, verify `SnackbarHost.Snackbar` is assigned to the injected singleton `ISnackbar`.
