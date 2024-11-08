namespace ControlR.Web.Client.Extensions;

public static class MessengerExtensions
{
  public static async Task SendToast(this IMessenger messenger, string message, Severity severity = Severity.Info)
  {
    await messenger.Send(new ToastMessage(message, severity));
  }
}