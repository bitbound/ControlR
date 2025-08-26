using Avalonia.Media;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using Avalonia;

namespace ControlR.DesktopClient.ViewModels.Fakes;

public class ToastWindowViewModelFake : IToastWindowViewModel
{
  public string Title { get; set; } = "Sample Toast Notification";
  public string Message { get; set; } = "This is a sample toast message that demonstrates how the toast notification will appear in the application.";
  public ToastIcon ToastIcon { get; set; } = ToastIcon.Info;

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
      var color = ToastIcon switch
      {
        ToastIcon.Info => "#3498DB",      // Blue
        ToastIcon.Success => "#2ECC71",   // Green
        ToastIcon.Warning => "#F39C12",   // Orange
        ToastIcon.Error => "#E74C3C",     // Red
        ToastIcon.Question => "#9B59B6",  // Purple
        _ => "#3498DB"                    // Default blue
      };
      
      return new SolidColorBrush(Color.Parse(color));
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

  public Func<Task>? OnClick { get; set; }
}
