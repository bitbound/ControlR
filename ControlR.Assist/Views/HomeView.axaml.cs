using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using ControlR.Assist.ViewModels;

namespace ControlR.Assist.Views;

public partial class HomeView : UserControl
{
  private int _copyFeedbackId;
  private HomeViewModel? _subscribedViewModel;

  public HomeView()
  {
    InitializeComponent();
    DetachedFromVisualTree += (_, _) => UnsubscribeFromViewModel();

    // The viewer-status run captures the secondary brush, so it has to be rebuilt when the
    // theme changes underneath it.
    ActualThemeVariantChanged += (_, _) => RebuildViewerStatus();
  }

  // SecondaryColor lives in the theme dictionaries, so the current theme variant must be passed.
  private IBrush? SecondaryBrush => this.TryFindResource("SecondaryColor", ActualThemeVariant, out var resource) ? resource as IBrush : null;

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);

    UnsubscribeFromViewModel();

    if (DataContext is HomeViewModel viewModel)
    {
      _subscribedViewModel = viewModel;
      viewModel.PropertyChanged += OnViewModelPropertyChanged;
      RebuildViewerStatus();
    }
  }

  private async void OnCopyCodeClick(object? sender, RoutedEventArgs e)
  {
    if (DataContext is not HomeViewModel { Code: { } code } || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
    {
      return;
    }

    try
    {
      await clipboard.SetTextAsync(code.Replace(" ", string.Empty));
    }
    catch (Exception)
    {
      // The platform clipboard can fail while another process holds it. Losing the copy feedback
      // is acceptable. Taking the app down mid-session is not.
      return;
    }

    var feedbackId = ++_copyFeedbackId;
    CopyGlyph.IsVisible = false;
    CopiedGlyph.IsVisible = true;
    await Task.Delay(TimeSpan.FromSeconds(1));

    if (feedbackId == _copyFeedbackId)
    {
      CopyGlyph.IsVisible = true;
      CopiedGlyph.IsVisible = false;
    }
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(HomeViewModel.ShowConnectedStatus) or nameof(HomeViewModel.ActiveViewerEmail))
    {
      RebuildViewerStatus();
    }
  }

  private void RebuildViewerStatus()
  {
    if (DataContext is not HomeViewModel { ShowConnectedStatus: true } viewModel)
    {
      return;
    }

    const string token = "{0}";
    var format = Localization.StatusConnectedToViewer;
    var tokenIndex = format.IndexOf(token, StringComparison.Ordinal);
    var prefix = tokenIndex >= 0 ? format[..tokenIndex] : format;
    var suffix = tokenIndex >= 0 ? format[(tokenIndex + token.Length)..] : string.Empty;

    var name = string.IsNullOrWhiteSpace(viewModel.ActiveViewerName)
      ? Localization.UnknownViewerLabel
      : viewModel.ActiveViewerName;

    var inlines = new InlineCollection();
    inlines.Add(new Run(prefix + name));
    inlines.Add(new Run($" ({viewModel.ActiveViewerEmail})") { Foreground = SecondaryBrush });
    inlines.Add(new Run(suffix));
    ViewerStatusText.Inlines = inlines;
  }

  private void UnsubscribeFromViewModel()
  {
    if (_subscribedViewModel is not null)
    {
      _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
      _subscribedViewModel = null;
    }
  }
}
