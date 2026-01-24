using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.DesktopClient.ViewModels;

public interface IToastWindowViewModel
{
  IBrush IconBrush { get; }
  StreamGeometry? IconGeometry { get; }
  string IconResourceKey { get; }
  string Message { get; set; }
  Func<Task>? OnClick { get; set; }
  string Title { get; set; }
  ToastIcon ToastIcon { get; set; }
}

public partial class ToastWindowViewModel : ObservableObject, IToastWindowViewModel
{

  [ObservableProperty]
  private string _message = string.Empty;
  [ObservableProperty]
  private string _title = string.Empty;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IconResourceKey), nameof(IconBrush), nameof(IconGeometry))]
  private ToastIcon _toastIcon;

  public IBrush IconBrush
  {
    get
    {
      var themeColor = ToastIcon switch
      {
        ToastIcon.Info => ThemeColor.Info,
        ToastIcon.Success => ThemeColor.Success,
        ToastIcon.Warning => ThemeColor.Warning,
        ToastIcon.Error => ThemeColor.Error,
        ToastIcon.Question => ThemeColor.Info,
        _ => ThemeColor.Info
      };

      if (ResourceLocator.TryGetThemeColorBrush(themeColor, out var themeBrush))
      {
        return themeBrush;
      }

      // Fallback to theme colors if custom colors aren't found
      var fallbackKey = ToastIcon switch
      {
        ToastIcon.Info => "SystemAccentColorDark1",
        ToastIcon.Success => "SystemFillColorSuccessBrush",
        ToastIcon.Warning => "SystemFillColorCautionBrush",
        ToastIcon.Error => "SystemFillColorCriticalBrush",
        ToastIcon.Question => "SystemAccentColorDark1",
        _ => "SystemAccentColorDark1"
      };

      if (ResourceLocator.TryGetThemedResource<IBrush>(fallbackKey, out var themeFallbackBrush))
      {
        return themeFallbackBrush;
      }

      // Final fallback to a solid color
      return new SolidColorBrush(Color.Parse("#3498DB"));
    }
  }

  public StreamGeometry? IconGeometry
  {
    get
    {
      if (ResourceLocator.TryGetResource<StreamGeometry>(IconResourceKey, out var geometry))
      {
        return geometry;
      }
      return null;
    }
  }

  public string IconResourceKey
  {
    get
    {
      return ToastIcon switch
      {
        ToastIcon.Info => "info_regular",
        ToastIcon.Success => "checkmark_circle_regular",
        ToastIcon.Warning => "warning_regular",
        ToastIcon.Error => "error_circle_regular",
        ToastIcon.Question => "question_circle_regular",
        _ => "info_regular"
      };
    }
  }

  public Func<Task>? OnClick { get; set; }
}
