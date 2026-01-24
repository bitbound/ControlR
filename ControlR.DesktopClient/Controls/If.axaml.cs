using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ControlR.DesktopClient.Controls;

public class If : ContentControl
{
  public static readonly StyledProperty<bool> ConditionProperty =
    AvaloniaProperty.Register<If, bool>(nameof(Condition));
  public static readonly StyledProperty<object?> FalseProperty =
    AvaloniaProperty.Register<If, object?>(nameof(False));
  public static readonly StyledProperty<object?> TrueProperty =
    AvaloniaProperty.Register<If, object?>(nameof(True));

  static If()
  {
    ConditionProperty.Changed.AddClassHandler<If>((x, e) => x.UpdateContent());
    TrueProperty.Changed.AddClassHandler<If>((x, e) => x.UpdateContent());
    FalseProperty.Changed.AddClassHandler<If>((x, e) => x.UpdateContent());
  }

  public bool Condition
  {
    get => GetValue(ConditionProperty);
    set => SetValue(ConditionProperty, value);
  }
  public object? False
  {
    get => GetValue(FalseProperty);
    set => SetValue(FalseProperty, value);
  }
  public object? True
  {
    get => GetValue(TrueProperty);
    set => SetValue(TrueProperty, value);
  }

  protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
  {
    base.OnApplyTemplate(e);
    UpdateContent();
  }

  private void UpdateContent()
  {
    Content = Condition ? True : False;
  }
}
