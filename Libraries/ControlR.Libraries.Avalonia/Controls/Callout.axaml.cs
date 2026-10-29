using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ControlR.Libraries.Avalonia.Controls;

public enum CalloutSeverity
{
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

  private PathIcon? _icon;
  private TextBlock? _messageText;

  static Callout()
  {
    MessageProperty.Changed.AddClassHandler<Callout>((x, _) => x.ApplyMessage());
    SeverityProperty.Changed.AddClassHandler<Callout>((x, _) => x.ApplySeverity());
  }

  public Callout()
  {
    InitializeComponent();
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
    if (_icon is null)
    {
      return;
    }

    var (className, data) = Severity switch
    {
      CalloutSeverity.Success => ("CalloutSuccess", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"),
      CalloutSeverity.Warning => ("CalloutWarning", "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"),
      CalloutSeverity.Error => ("CalloutError", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"),
      _ => ("CalloutInfo", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z")
    };

    foreach (var cls in new[] { "CalloutInfo", "CalloutSuccess", "CalloutWarning", "CalloutError" })
    {
      _icon.Classes.Remove(cls);
    }

    _icon.Classes.Add(className);
    _icon.Data = Geometry.Parse(data);
  }

  private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
