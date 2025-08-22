using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using Avalonia;

namespace ControlR.DesktopClient.ViewModels;

public interface IToastWindowViewModel
{
  string Title { get; set; }
  string Message { get; set; }
  ToastIcon ToastIcon { get; set; }
  string IconResourceKey { get; }
  IBrush IconBrush { get; }
  StreamGeometry? IconGeometry { get; }
}

public partial class ToastWindowViewModel : ObservableObject, IToastWindowViewModel
{
  [ObservableProperty]
  private string _title = string.Empty;

  [ObservableProperty]
  private string _message = string.Empty;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IconResourceKey), nameof(IconBrush), nameof(IconGeometry))]
  private ToastIcon _toastIcon;

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

  public IBrush IconBrush
  {
    get
    {
      var resourceKey = ToastIcon switch
      {
        ToastIcon.Info => "InfoColor",
        ToastIcon.Success => "SuccessColor",
        ToastIcon.Warning => "WarningColor",
        ToastIcon.Error => "ErrorColor",
        ToastIcon.Question => "InfoColor",
        _ => "InfoColor"
      };
      
      // Try to get the brush from application resources
      if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var resource) == true 
          && resource is IBrush brush)
      {
        return brush;
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
      
      if (Application.Current?.Resources.TryGetResource(fallbackKey, null, out var fallbackResource) == true 
          && fallbackResource is IBrush fallbackBrush)
      {
        return fallbackBrush;
      }
      
      // Final fallback to a solid color
      return new SolidColorBrush(Color.Parse("#3498DB"));
    }
  }

  public StreamGeometry? IconGeometry
  {
    get
    {
      if (Application.Current?.Resources.TryGetResource(IconResourceKey, null, out var resource) == true 
          && resource is StreamGeometry geometry)
      {
        return geometry;
      }
      return null;
    }
  }
}
