using MudBlazor;

namespace ControlR.Viewer.Extensions;
public static class IMessengerExtensions
{
    public static async Task SendToast(this IMessenger messenger, string message, Severity severity = Severity.Info)
    {
        await messenger.Send(new ToastMessage(message, severity));
    }
}
