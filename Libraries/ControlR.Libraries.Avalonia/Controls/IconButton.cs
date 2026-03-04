using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ControlR.Libraries.Avalonia.Controls;

public class IconButton : ThemeButton
{
  public static readonly StyledProperty<double> ContentSpacingProperty =
    AvaloniaProperty.Register<IconButton, double>(nameof(ContentSpacing), 8);
  public static readonly StyledProperty<Geometry?> IconProperty =
    AvaloniaProperty.Register<IconButton, Geometry?>(nameof(Icon));
  public static readonly StyledProperty<double> IconSizeProperty =
    AvaloniaProperty.Register<IconButton, double>(nameof(IconSize), 16);
  public static readonly StyledProperty<string?> TextProperty =
    AvaloniaProperty.Register<IconButton, string?>(nameof(Text));

  static IconButton()
  {
    ContentSpacingProperty.Changed.AddClassHandler<IconButton>(OnContentPropertyChanged);
    IconProperty.Changed.AddClassHandler<IconButton>(OnContentPropertyChanged);
    IconSizeProperty.Changed.AddClassHandler<IconButton>(OnContentPropertyChanged);
    TextProperty.Changed.AddClassHandler<IconButton>(OnContentPropertyChanged);
  }

  public IconButton()
  {
    UpdateContent();
  }

  public double ContentSpacing
  {
    get => GetValue(ContentSpacingProperty);
    set => SetValue(ContentSpacingProperty, value);
  }

  public Geometry? Icon
  {
    get => GetValue(IconProperty);
    set => SetValue(IconProperty, value);
  }

  public double IconSize
  {
    get => GetValue(IconSizeProperty);
    set => SetValue(IconSizeProperty, value);
  }

  public string? Text
  {
    get => GetValue(TextProperty);
    set => SetValue(TextProperty, value);
  }

  protected override Type StyleKeyOverride => typeof(IconButton);

  private static void OnContentPropertyChanged(IconButton sender, AvaloniaPropertyChangedEventArgs e)
  {
    sender.UpdateContent();
  }

  private void UpdateContent()
  {
    var stackPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = ContentSpacing,
      VerticalAlignment = VerticalAlignment.Center
    };

    if (Icon is not null)
    {
      stackPanel.Children.Add(new PathIcon
      {
        Data = Icon,
        Width = IconSize,
        Height = IconSize,
        VerticalAlignment = VerticalAlignment.Center,
        Classes = { Color.ToString() }
      });
    }

    if (!string.IsNullOrWhiteSpace(Text))
    {
      stackPanel.Children.Add(new TextBlock
      {
        Text = Text,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = FontSize,
        Classes = { Color.ToString() }
      });
    }

    Content = stackPanel;
  }
}