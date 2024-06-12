#nullable enable

using ControlR.Viewer.Services;

namespace ControlR.Viewer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new MainPage();
        MainPage.Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object? sender, EventArgs e)
    {
        if (sender is MainPage mainPage)
        {
            mainPage.Window.Destroying += MainWindow_Destroying;

            if (Handler.MauiContext?.Services is not IServiceProvider services)
            {
                await mainPage.DisplayAlert("Init Failure", "MAUI services are unexpectedly null.  Startup cannot continue.", "OK");
                return;
            }

            var appState = services.GetRequiredService<IAppState>();
            var viewerHub = services.GetRequiredService<IViewerHubConnection>();
            await viewerHub.Start(appState.AppExiting);
        }
    }

    private void MainWindow_Destroying(object? sender, EventArgs e)
    {
        if (Handler?.MauiContext?.Services is null)
        {
            return;
        }

        var messenger = Handler.MauiContext.Services.GetRequiredService<IMessenger>();
        messenger.SendGenericMessage(GenericMessageKind.ShuttingDown);
    }
}