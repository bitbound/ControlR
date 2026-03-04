using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.ApiClient;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Avalonia.ViewModels;
using ControlR.Viewer.Avalonia.Views.Dialogs;

namespace ControlR.Viewer.Avalonia.ViewModels.Dialogs;

public interface IDesktopPreviewDialogViewModel : IDisposable, IViewReference<DesktopPreviewDialogView>
{
  IRelayCommand CloseCommand { get; }
  string? ErrorMessage { get; }
  bool HasError { get; }
  bool HasImage { get; }
  bool IsLoading { get; }
  Bitmap? PreviewImageSource { get; }
  IAsyncRelayCommand RefreshCommand { get; }

  Task LoadPreview(bool showSuccessSnackbar);
}

public partial class DesktopPreviewDialogViewModel : ViewModelBase<DesktopPreviewDialogView>, IDesktopPreviewDialogViewModel
{
  private readonly IControlrApi _apiClient;
  private readonly IDialogProvider _dialogProvider;
  private readonly ILogger<DesktopPreviewDialogViewModel> _logger;
  private readonly DesktopSession _session;
  private readonly ISnackbar _snackbar;
  private readonly IOptions<ControlrViewerOptions> _viewerOptions;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasError))]
  private string? _errorMessage;
  [ObservableProperty]
  private bool _isLoading;
  private Bitmap? _previewImageSource;

  public DesktopPreviewDialogViewModel(
    DesktopSession session,
    IControlrApi apiClient,
    IDialogProvider dialogProvider,
    ISnackbar snackbar,
    IOptions<ControlrViewerOptions> viewerOptions,
    ILogger<DesktopPreviewDialogViewModel> logger)
  {
    _apiClient = apiClient;
    _dialogProvider = dialogProvider;
    _snackbar = snackbar;
    _viewerOptions = viewerOptions;
    _logger = logger;
    _session = session;
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool HasImage => PreviewImageSource is not null;
  public Bitmap? PreviewImageSource
  {
    get => _previewImageSource;
    private set
    {
      if (ReferenceEquals(_previewImageSource, value))
      {
        return;
      }

      _previewImageSource?.Dispose();
      _previewImageSource = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasImage));
    }
  }

  [RelayCommand]
  public async Task LoadPreview(bool showSuccessSnackbar)
  {
    try
    {
      IsLoading = true;
      ErrorMessage = null;
      PreviewImageSource?.Dispose();
      PreviewImageSource = null;

      var result = await _apiClient.DesktopPreview.GetDesktopPreview(
        _viewerOptions.Value.DeviceId,
        _session.ProcessId);

      if (result.IsSuccess && result.Value.Length > 0)
      {
        using var imageStream = new MemoryStream(result.Value);
        PreviewImageSource = new Bitmap(imageStream);

        if (showSuccessSnackbar)
        {
          _snackbar.Add(Resources.RemoteControl_PreviewRefreshed, SnackbarSeverity.Info);
        }

        return;
      }

      ErrorMessage = result.IsSuccess
        ? Resources.RemoteControl_NoPreviewImage
        : result.Reason;
      _logger.LogWarning(
        "Failed to get desktop preview for device {DeviceId}, process {ProcessId}. Reason: {Reason}",
        _viewerOptions.Value.DeviceId,
        _session.ProcessId,
        ErrorMessage);
    }
    catch (Exception ex)
    {
      _logger.LogError(
        ex,
        "Error while loading desktop preview for device {DeviceId}, process {ProcessId}",
        _viewerOptions.Value.DeviceId,
        _session.ProcessId);
      ErrorMessage = Resources.RemoteControl_PreviewLoadFailed;
    }
    finally
    {
      IsLoading = false;
    }
  }

  [RelayCommand]
  public async Task Refresh()
  {
    await LoadPreview(showSuccessSnackbar: true);
  }

  protected override void Dispose(bool disposing)
  {
    PreviewImageSource?.Dispose();
    PreviewImageSource = null;
    base.Dispose(disposing);
  }

  [RelayCommand]
  private void Close()
  {
    _dialogProvider.Close();
  }
}
