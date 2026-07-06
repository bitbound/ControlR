using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ControlR.AvaloniaViewerExample.ViewModels;
using ControlR.AvaloniaViewerExample.ViewModels.Fakes;
using ControlR.AvaloniaViewerExample.Views;
using ControlR.Libraries.Viewer.Common.Options;
using Microsoft.Extensions.Configuration;

namespace ControlR.AvaloniaViewerExample;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    RequestedThemeVariant = ThemeVariant.Dark;

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      IMainWindowViewModel viewModel = Design.IsDesignMode
        ? GetFakeViewModel()
        : GetActualViewModel();

      desktop.MainWindow = new MainWindow()
      {
        // This is just an example.
        DataContext = viewModel
      };
    }

    base.OnFrameworkInitializationCompleted();
  }

  private MainWindowViewModel GetActualViewModel()
  {
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
    // Load configuration from user secrets and appsettings.json
    var configuration = new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", optional: true)
      .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
      .AddUserSecrets<App>(optional: true)
      .Build();

    // Get viewer options from configuration.
    // This is just an example. You can use whatever method you prefer  (e.g. DI with IOptions pattern) 
    // for getting these options into the ControlrViewer component.
    var viewerOptions = new ControlrViewerOptions
    {

      AuthenticationMethod = ViewerAuthenticationMethod.PersonalAccessToken,
      BaseUrl = Uri.TryCreate(configuration["ControlrViewerOptions:BaseUrl"], UriKind.Absolute, out var baseUrl)
        ? baseUrl
        : throw new InvalidOperationException("ControlrViewerOptions:BaseUrl not configured. Use: dotnet user-secrets set \"ControlrViewerOptions:BaseUrl\" \"https://controlr.example.com\""),
      DeviceId = Guid.Parse(configuration["ControlrViewerOptions:DeviceId"]
        ?? throw new InvalidOperationException("ControlrViewerOptions:DeviceId not configured. Use: dotnet user-secrets set \"ControlrViewerOptions:DeviceId\" \"your-device-guid\"")),
      PersonalAccessToken = configuration["ControlrViewerOptions:PersonalAccessToken"]
    };

    return new MainWindowViewModel(viewerOptions);
  }

  private MainWindowViewModelFake GetFakeViewModel()
  {
    return new MainWindowViewModelFake();
  }
}
