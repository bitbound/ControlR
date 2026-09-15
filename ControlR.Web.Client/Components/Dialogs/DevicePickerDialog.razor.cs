using ControlR.Libraries.Api.Contracts.FilterSort;

namespace ControlR.Web.Client.Components.Dialogs;

public partial class DevicePickerDialog : ComponentBase
{
  private const int PageSize = 10;

  private int _currentPage = 1;
  private List<InternalDtos.DeviceResponseDto> _devices = [];
  private bool _loading;
  private string _searchText = string.Empty;
  private InternalDtos.DeviceResponseDto? _selectedDevice;
  private int _totalPages = 1;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [CascadingParameter]
  public required IMudDialogInstance MudDialog { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await LoadDevices();
  }

  private void Cancel() => MudDialog.Cancel();

  private void HandleSelectedChanged(InternalDtos.DeviceResponseDto? device)
  {
    _selectedDevice = device;
  }

  private async Task LoadDevices()
  {
    _loading = true;
    await InvokeAsync(StateHasChanged);

    try
    {
      var request = new InternalDtos.DeviceSearchRequestDto
      {
        SearchText = _searchText,
        HideOfflineDevices = false,
        Page = _currentPage - 1,
        PageSize = PageSize,
        SortDefinitions = [new DeviceColumnSort { PropertyName = nameof(InternalDtos.DeviceResponseDto.Name), Descending = false, SortOrder = 0 }]
      };

      var response = await ControlrApi.Internal.Devices.SearchDevices(request);
      if (!response.IsSuccess)
      {
        Snackbar.Add("Failed to load devices", Severity.Error);
        return;
      }

      _devices = [.. response.Value.Items ?? []];
      _totalPages = Math.Max(1, (int)Math.Ceiling(response.Value.TotalItems / (double)PageSize));
    }
    finally
    {
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task OnPageChanged(int page)
  {
    _currentPage = page;
    await LoadDevices();
  }

  private async Task OnSearchChanged(string _)
  {
    _currentPage = 1;
    await LoadDevices();
  }

  private void Select()
  {
    if (_selectedDevice is null)
    {
      return;
    }

    MudDialog.Close(DialogResult.Ok(_selectedDevice));
  }
}
