using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace ControlR.Viewer.Services;

internal interface INotificationProvider
{
    Task DisplayAlert(string title, string message, string cancelText);

    Task<bool> DisplayAlert(string title, string message, string acceptText, string cancelText);

    Task DisplaySnackbar(
        string message,
        Action? action = null,
        string actionButtonText = "OK",
        TimeSpan? duration = null,
        SnackbarOptions? visualOptions = null,
        CancellationToken cancellationToken = default);
}

internal class NotificationProvider : INotificationProvider
{
    public Task DisplayAlert(string title, string message, string cancelText)
    {
        return MainPage.Current.DisplayAlert(title, message, cancelText);
    }

    public Task<bool> DisplayAlert(string title, string message, string acceptText, string cancelText)
    {
        return MainPage.Current.DisplayAlert(title, message, acceptText, cancelText);
    }

    public Task DisplaySnackbar(
        string message,
        Action? action = null,
        string actionButtonText = "OK",
        TimeSpan? duration = null,
        SnackbarOptions? visualOptions = null,
        CancellationToken cancellationToken = default)
    {
        return MainPage.Current.DisplaySnackbar(message, action, actionButtonText, duration, visualOptions, cancellationToken);
    }
}