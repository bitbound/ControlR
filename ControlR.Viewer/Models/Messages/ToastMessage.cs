using MudBlazor;

namespace ControlR.Viewer.Models.Messages;

/// <summary>
/// This message allows using <see cref="ISnackbar"/> outside of UI elements.
/// If injecting the <see cref="ISnackbar"/> interface into a service that 
/// gets instantiated before the UI is ready, the Snackbar ctor will throw.
/// </summary>
/// <param name="message"></param>
/// <param name="severity"></param>
internal class ToastMessage(string message, Severity severity)
{
    public string Message { get; } = message;
    public Severity Severity { get; } = severity;
}
