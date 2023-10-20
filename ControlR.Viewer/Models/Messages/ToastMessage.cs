using MudBlazor;

namespace ControlR.Viewer.Models.Messages;
internal class ToastMessage(string message, Severity severity)
{
    public string Message { get; } = message;
    public Severity Severity { get; } = severity;
}
