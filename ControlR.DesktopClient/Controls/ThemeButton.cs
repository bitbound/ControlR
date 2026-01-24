using Avalonia;
using Avalonia.Controls;

namespace ControlR.DesktopClient.Controls;

public class ThemeButton : Button
{
  public static readonly StyledProperty<ThemeColor> ColorProperty =
      AvaloniaProperty.Register<ThemeButton, ThemeColor>(nameof(Color), ThemeColor.Default);
  public static readonly StyledProperty<ControlThemeVariant> VariantProperty =
      AvaloniaProperty.Register<ThemeButton, ControlThemeVariant>(nameof(Variant), ControlThemeVariant.Filled);

  static ThemeButton()
  {
    ColorProperty.Changed.AddClassHandler<ThemeButton>(OnColorPropertyChanged);
    VariantProperty.Changed.AddClassHandler<ThemeButton>(OnVariantPropertyChanged);
  }

  public ThemeButton()
  {
    // Ensure classes reflect initial values (covers design-time/initialization)
    UpdateColorClass(Color, add: true);
    UpdateVariantClass(Variant, add: true);

    // Re-apply classes when attached in case theme/styles were applied later
    AttachedToVisualTree += (s, e) =>
    {
      UpdateColorClass(Color, add: true);
      UpdateVariantClass(Variant, add: true);
    };
  }

  public ThemeColor Color
  {
    get => GetValue(ColorProperty);
    set => SetValue(ColorProperty, value);
  }
  public ControlThemeVariant Variant
  {
    get => GetValue(VariantProperty);
    set => SetValue(VariantProperty, value);
  }

  protected override Type StyleKeyOverride => typeof(Button);

  private static void OnColorPropertyChanged(ThemeButton sender, AvaloniaPropertyChangedEventArgs e)
  {
    // Safely extract old/new values with fallback to current property on the instance
    var oldValue = e.OldValue is ThemeColor vold ? vold : sender.Color;
    var newValue = e.NewValue is ThemeColor vnew ? vnew : sender.Color;
    sender.OnColorChanged(oldValue, newValue);
  }

  private static void OnVariantPropertyChanged(ThemeButton sender, AvaloniaPropertyChangedEventArgs e)
  {
    var oldValue = e.OldValue is ControlThemeVariant vold ? vold : sender.Variant;
    var newValue = e.NewValue is ControlThemeVariant vnew ? vnew : sender.Variant;
    sender.OnVariantChanged(oldValue, newValue);
  }

  private void OnColorChanged(ThemeColor oldValue, ThemeColor newValue)
  {
    UpdateColorClass(oldValue, add: false);
    UpdateColorClass(newValue, add: true);
  }

  private void OnVariantChanged(ControlThemeVariant oldValue, ControlThemeVariant newValue)
  {
    UpdateVariantClass(oldValue, add: false);
    UpdateVariantClass(newValue, add: true);
  }

  private void UpdateColorClass(ThemeColor color, bool add)
  {
    if (color == ThemeColor.Default)
      return;

    var cls = color.ToString();
    if (add)
    {
      if (!Classes.Contains(cls))
        Classes.Add(cls);
    }
    else
    {
      Classes.Remove(cls);
    }
  }

  private void UpdateVariantClass(ControlThemeVariant variant, bool add)
  {
    var cls = variant switch
    {
      ControlThemeVariant.Filled => "VariantFilled",
      ControlThemeVariant.Outlined => "VariantOutlined",
      ControlThemeVariant.Text => "VariantText",
      _ => variant.ToString()
    };

    if (add)
    {
      if (!Classes.Contains(cls))
        Classes.Add(cls);
    }
    else
    {
      Classes.Remove(cls);
    }
  }
}