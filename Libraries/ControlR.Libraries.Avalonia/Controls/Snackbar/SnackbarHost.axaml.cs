using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ControlR.Libraries.Avalonia.Controls.Snackbar;

public partial class SnackbarHost : UserControl
{
  public static readonly StyledProperty<ISnackbar?> SnackbarProperty =
    AvaloniaProperty.Register<SnackbarHost, ISnackbar?>(nameof(Snackbar));

  static SnackbarHost()
  {
    SnackbarProperty.Changed.AddClassHandler<SnackbarHost>(OnSnackbarChanged);
  }

  public SnackbarHost()
  {
    DataContext = this;
    InitializeComponent();
    ApplyPosition();
  }

  public ISnackbar? Snackbar
  {
    get => GetValue(SnackbarProperty);
    set => SetValue(SnackbarProperty, value);
  }

  private static void OnSnackbarChanged(SnackbarHost sender, AvaloniaPropertyChangedEventArgs e)
  {
    sender.ApplyPosition();
  }

  private static void UpdateSeverityClasses(Border border, SnackbarSeverity severity)
  {
    border.Classes.Remove("SeverityInformation");
    border.Classes.Remove("SeverityWarning");
    border.Classes.Remove("SeverityError");
    border.Classes.Remove("SeveritySuccess");

    var severityClass = severity switch
    {
      SnackbarSeverity.Warning => "SeverityWarning",
      SnackbarSeverity.Error => "SeverityError",
      SnackbarSeverity.Success => "SeveritySuccess",
      _ => "SeverityInformation"
    };

    border.Classes.Add(severityClass);
  }

  private void ApplyPosition()
  {
    var position = Snackbar?.Options.Position ?? SnackbarPosition.BottomRight;
    var (horizontalAlignment, verticalAlignment) = position switch
    {
      SnackbarPosition.TopLeft => (HorizontalAlignment.Left, VerticalAlignment.Top),
      SnackbarPosition.TopCenter => (HorizontalAlignment.Center, VerticalAlignment.Top),
      SnackbarPosition.TopRight => (HorizontalAlignment.Right, VerticalAlignment.Top),
      SnackbarPosition.BottomLeft => (HorizontalAlignment.Left, VerticalAlignment.Bottom),
      SnackbarPosition.BottomCenter => (HorizontalAlignment.Center, VerticalAlignment.Bottom),
      _ => (HorizontalAlignment.Right, VerticalAlignment.Bottom)
    };

    HorizontalAlignment = horizontalAlignment;
    VerticalAlignment = verticalAlignment;
  }

  private void OnDismissClicked(object? sender, RoutedEventArgs e)
  {
    if (sender is not Button { Tag: Guid messageId })
    {
      return;
    }

    Snackbar?.Remove(messageId);
  }

  private void OnMessageBorderLoaded(object? sender, RoutedEventArgs e)
  {
    if (sender is not Border border)
    {
      return;
    }

    border.Transitions =
    [
      new DoubleTransition
      {
        Property = OpacityProperty,
        Duration = Snackbar?.Options.FadeDuration ?? TimeSpan.FromSeconds(0.5)
      }
    ];

    if (border.DataContext is SnackbarMessage message)
    {
      UpdateSeverityClasses(border, message.Severity);
    }
  }

  private void OnMessageDataContextChanged(object? sender, EventArgs e)
  {
    if (sender is not Border border || border.DataContext is not SnackbarMessage message)
    {
      return;
    }

    UpdateSeverityClasses(border, message.Severity);
  }
}