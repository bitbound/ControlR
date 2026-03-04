# ControlR.Libraries.Avalonia

A cross-platform Avalonia UI library providing reusable controls, theming, and view model utilities for ControlR desktop applications.

## Features

### Controls

- **IconButton**: A button control that supports both icon and text content with configurable spacing
- **ThemeButton**: Base button control with theming support
- **DialogHost** & **DialogProvider**: A dialog management system for Avalonia applications
- **Snackbar**: A notification system for displaying temporary messages

### Theming

- **ThemeMode**: Support for light, dark, and system theme modes
- **ThemeColor**: Predefined color palette for consistent styling
- **ThemeColorKeys**: Centralized color key definitions
- **ControlThemeVariant**: Theme variant handling

### ViewModels

- **ObservableObject**: Base class for view models with property change notification
- **IViewReference**: Interface for view reference management

### Resources

- **Styles.axaml**: Global styles for the application
- **Theme.axaml**: Theme resource definitions

## Usage

### Adding the Library

Reference the library in your Avalonia project:

```xml
<ProjectReference Include="..\ControlR.Libraries.Avalonia\ControlR.Libraries.Avalonia.csproj" />
```

### Using Controls

#### IconButton

```xml
<controls:IconButton Icon="{StaticResource MyIcon}"
                     Text="Click Me"
                     IconSize="16"
                     ContentSpacing="8"/>
```

#### Dialogs

```csharp
// Register dialogs
services.AddDialogs();

// Show a dialog
var result = await dialogHost.ShowAsync<MyDialogViewModel, MyDialogResult>();
```

#### Snackbar

```csharp
// Register snackbar services
services.AddSnackbar();

// Show a message
snackbarService.Show("Operation completed", SnackbarSeverity.Success);
```

### Theming

```csharp
// Set theme mode
ThemeMode.Current = ThemeMode.Dark;

// Access theme colors
var primaryColor = ThemeColor.GetColor(ThemeColorKeys.Primary);
```

## Architecture

The library follows a modular structure:

- `Controls/` - Reusable UI controls
- `Theming/` - Theme management and color definitions
- `ViewModels/` - Base view model classes and interfaces
- `Resources/` - XAML styles and theme resources
