using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using ControlR.Assist.ViewModels;

namespace ControlR.Assist.Views;

public partial class MainWindow : Window
{
  private bool _exitConfirmed;

  // The parameterless overload exists so the XAML loader can reach this window by avares URI.
  public MainWindow()
  {
    InitializeComponent();
  }

  public MainWindow(MainWindowViewModel viewModel)
    : this()
  {
    DataContext = viewModel;
    viewModel.Home.PropertyChanged += OnHomePropertyChanged;
  }

  private HomeViewModel Home => ((MainWindowViewModel)DataContext!).Home;

  // SecondaryColor lives in the theme dictionaries, so the current theme variant must be passed.
  private IBrush? SecondaryBrush => this.TryFindResource("SecondaryColor", ActualThemeVariant, out var resource) ? resource as IBrush : null;

  protected override async void OnClosing(WindowClosingEventArgs e)
  {
    var sessionInProgress = Home.IsConsentPending || Home.IsSessionActive;

    if (sessionInProgress && !_exitConfirmed)
    {
      e.Cancel = true;

      if (await ((MainWindowViewModel)DataContext!).ConfirmExitAsync())
      {
        _exitConfirmed = true;
        Close();
      }

      return;
    }

    base.OnClosing(e);
  }

  private void OnHomePropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    switch (e.PropertyName)
    {
      case nameof(HomeViewModel.IsConsentPending):
        Dispatcher.UIThread.Post(UpdateConsentModal);
        break;
      case nameof(HomeViewModel.ConsentViewerName):
      case nameof(HomeViewModel.ConsentEmail):
        Dispatcher.UIThread.Post(() =>
        {
          if (Home.IsConsentPending)
          {
            RebuildConsentText();
          }
        });
        break;
    }
  }

  private void RebuildConsentText()
  {
    const string token = "{0}";
    var format = Localization.ConsentPromptFormat;
    var tokenIndex = format.IndexOf(token, StringComparison.Ordinal);
    var prefix = tokenIndex >= 0 ? format[..tokenIndex] : format;
    var suffix = tokenIndex >= 0 ? format[(tokenIndex + token.Length)..] : string.Empty;

    var name = string.IsNullOrWhiteSpace(Home.ConsentViewerName)
      ? Localization.UnknownViewerLabel
      : Home.ConsentViewerName;

    var inlines = new InlineCollection();
    inlines.Add(new Run(prefix + name) { FontWeight = FontWeight.SemiBold });

    if (Home.ShowConsentEmail && Home.ConsentEmail is { } email)
    {
      inlines.Add(new Run($" ({email})") { Foreground = SecondaryBrush });
    }

    inlines.Add(new Run(suffix));
    ConsentText.Inlines = inlines;
  }

  private void UpdateConsentModal()
  {
    var pending = Home.IsConsentPending;
    Topmost = pending;

    if (pending)
    {
      RebuildConsentText();

      // Activate() does not restore a minimized window, and the customer may have minimized
      // Assist after the technician entered the code.
      WindowState = WindowState.Normal;
      Activate();
    }
  }
}
