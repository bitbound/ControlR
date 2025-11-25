namespace ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;

public interface IToaster
{
  Task ShowToast(string title, string message, ToastIcon toastIcon, TimeSpan? closeAfter = null);
  Task ShowToast(string title, string message, ToastIcon toastIcon, Func<Task> onClick, TimeSpan? closeAfter = null);
  Task ShowToast(string title, string message, ToastIcon toastIcon, Action onClick, TimeSpan? closeAfter = null);
}
