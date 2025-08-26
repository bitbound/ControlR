namespace ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;

public interface IToaster
{
  Task ShowToast(string title, string message, ToastIcon toastIcon);
  Task ShowToast(string title, string message, ToastIcon toastIcon, Func<Task> onClick);
  Task ShowToast(string title, string message, ToastIcon toastIcon, Action onClick);
}
