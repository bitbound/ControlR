using ControlR.DesktopClient.Common.Models;

namespace ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
public interface IToaster
{
  Task ShowToast(string title, string message, ToastIcon toastIcon);
}
