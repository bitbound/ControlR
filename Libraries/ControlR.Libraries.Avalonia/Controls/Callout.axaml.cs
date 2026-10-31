using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ControlR.Libraries.Avalonia.Controls;

public enum CalloutSeverity
{
  Primary,
  Secondary,
  Tertiary,
  Info,
  Success,
  Warning,
  Error
}

/// <summary>
/// A tinted, icon-accented banner for notices, warnings, and errors.
/// </summary>
public sealed partial class Callout : UserControl
{
  public static readonly StyledProperty<string?> MessageProperty =
    AvaloniaProperty.Register<Callout, string?>(nameof(Message));

  public static readonly StyledProperty<CalloutSeverity> SeverityProperty =
    AvaloniaProperty.Register<Callout, CalloutSeverity>(nameof(Severity), CalloutSeverity.Info);

  private static readonly string[] _severityClassNames =
  [
    "CalloutPrimary",
    "CalloutSecondary",
    "CalloutTertiary",
    "CalloutInfo",
    "CalloutSuccess",
    "CalloutWarning",
    "CalloutError"
  ];

  private PathIcon? _icon;
  private TextBlock? _messageText;
  private Border? _root;

  static Callout()
  {
    MessageProperty.Changed.AddClassHandler<Callout>((x, _) => x.ApplyMessage());
    SeverityProperty.Changed.AddClassHandler<Callout>((x, _) => x.ApplySeverity());
  }

  public Callout()
  {
    InitializeComponent();
    _root = this.FindControl<Border>("Root");
    _icon = this.FindControl<PathIcon>("Icon");
    _messageText = this.FindControl<TextBlock>("MessageText");
    ApplySeverity();
    ApplyMessage();
  }

  public string? Message
  {
    get => GetValue(MessageProperty);
    set => SetValue(MessageProperty, value);
  }

  public CalloutSeverity Severity
  {
    get => GetValue(SeverityProperty);
    set => SetValue(SeverityProperty, value);
  }

  private void ApplyMessage()
  {
    if (_messageText is not null)
    {
      _messageText.Text = Message;
    }
  }

  private void ApplySeverity()
  {
    if (_root is null || _icon is null)
    {
      return;
    }

    var (className, data) = Severity switch
    {
      CalloutSeverity.Primary => ("CalloutPrimary", "M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z"),
      CalloutSeverity.Secondary => ("CalloutSecondary", "M22 9.24l-7.19-.62L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21 12 17.27 18.18 21l-1.63-7.03L22 9.24zM12 15.4l-3.76 2.27 1-4.28-3.32-2.88 4.38-.38L12 6.1l1.71 4.04 4.38.38-3.32 2.88 1 4.28L12 15.4z"),
      CalloutSeverity.Tertiary => ("CalloutTertiary", "M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8-8-8z"),
      CalloutSeverity.Success => ("CalloutSuccess", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"),
      CalloutSeverity.Warning => ("CalloutWarning", "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"),
      CalloutSeverity.Error => ("CalloutError", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"),
      _ => ("CalloutInfo", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z")
    };

    // The severity class must go on the Border itself, not just the icon.
    // The tint and text-color styles key off "Border.CalloutX" selectors.
    foreach (var cls in _severityClassNames)
    {
      _root.Classes.Remove(cls);
      _icon.Classes.Remove(cls);
    }

    _root.Classes.Add(className);
    _icon.Classes.Add(className);
    _icon.Data = Geometry.Parse(data);
  }

  private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
