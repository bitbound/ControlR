using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

namespace ControlR.Web.Client.Components.Shared;

public partial class AuthorizationLogsPanel
{
  private string? _actionTypeFilter;
  private string? _actorTypeFilter;
  private Guid _callerTenantId;
  private AuthorizationChangeLogDto? _expandedItem;
  private DateTime? _fromDate;
  private bool _hasTenantContext;
  private bool _isLoading;
  private string _searchText = string.Empty;
  private Guid? _selectedTenantId;
  private MudTable<AuthorizationChangeLogDto>? _table;
  private string? _targetTypeFilter;
  private TenantSummaryDto[] _tenants = [];
  private DateTime? _toDate;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required ILogger<AuthorizationLogsPanel> Logger { get; init; }

  [Parameter]
  public bool ShowTenantFilter { get; set; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var callerTenantId))
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
      return;
    }

    _callerTenantId = callerTenantId;
    _hasTenantContext = true;
    await LoadTenants();
  }

  private static string FormatId(Guid? id) =>
    id is { } value ? value.ToString("D") : "—";

  private static IEnumerable<string> SearchVocabulary(IReadOnlyList<string> values, string query) =>
    string.IsNullOrWhiteSpace(query)
      ? values
      : values.Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase));

  private async Task ApplyFilters()
  {
    if (_table is null)
    {
      return;
    }

    await _table.ReloadServerData();
  }

  private string GetTenantName(Guid? tenantId)
  {
    // Tenant-scoped rows always carry OwningTenantId. The server-scoped rows live behind a
    // separate endpoint this panel never calls. The old "(server)" label here was misleading.
    if (tenantId is null)
    {
      return "—";
    }

    var tenant = _tenants.FirstOrDefault(x => x.Id == tenantId.Value);
    return tenant?.Name ?? tenantId.Value.ToString();
  }

  private async Task<TableData<AuthorizationChangeLogDto>> LoadTableData(
    TableState state, CancellationToken cancellationToken)
  {
    if (!_hasTenantContext)
    {
      return new TableData<AuthorizationChangeLogDto> { Items = [], TotalItems = 0 };
    }

    _isLoading = true;
    try
    {
      var result = await ControlrApi.V1.AuthorizationChangeLogs.GetAuthorizationChangeLogs(
        tenantId: _selectedTenantId ?? _callerTenantId,
        page: state.Page,
        pageSize: state.PageSize,
        actionType: string.IsNullOrWhiteSpace(_actionTypeFilter) ? null : _actionTypeFilter.Trim(),
        actorType: string.IsNullOrWhiteSpace(_actorTypeFilter) ? null : _actorTypeFilter.Trim(),
        targetType: string.IsNullOrWhiteSpace(_targetTypeFilter) ? null : _targetTypeFilter.Trim(),
        searchText: string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim(),
        from: _fromDate is { } from ? new DateTimeOffset(from) : null,
        to: _toDate is { } to ? new DateTimeOffset(to).AddDays(1) : null,
        cancellationToken: cancellationToken);

      if (!result.IsSuccess)
      {
        Snackbar.Add($"Failed to load authorization logs: {result.Reason}", Severity.Error);
        return new TableData<AuthorizationChangeLogDto> { Items = [], TotalItems = 0 };
      }

      return new TableData<AuthorizationChangeLogDto>
      {
        Items = result.Value.Items,
        TotalItems = result.Value.TotalItems
      };
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading authorization logs.");
      Snackbar.Add($"Error loading authorization logs: {ex.Message}", Severity.Error);
      return new TableData<AuthorizationChangeLogDto> { Items = [], TotalItems = 0 };
    }
    finally
    {
      _isLoading = false;
    }
  }

  private async Task LoadTenants()
  {
    try
    {
      var result = await ControlrApi.V1.Tenants.GetAllTenants();
      if (result.IsSuccess)
      {
        _tenants = [.. result.Value.Items];
      }
    }
    catch (Exception ex)
    {
      Logger.LogDebug(ex, "Tenant list unavailable; hiding tenant filter.");
    }
  }

  private async Task Refresh()
  {
    if (_table is null)
    {
      return;
    }

    await _table.ReloadServerData();
    Snackbar.Add("Authorization logs refreshed", Severity.Success);
  }

  private Task<IEnumerable<string>> SearchActionTypes(string query, CancellationToken cancellationToken) =>
    Task.FromResult(SearchVocabulary(ChangeLogVocabulary.ActionTypes, query));

  private Task<IEnumerable<string>> SearchTargetTypes(string query, CancellationToken cancellationToken) =>
    Task.FromResult(SearchVocabulary(ChangeLogVocabulary.TargetTypes, query));

  private void ToggleExpanded(AuthorizationChangeLogDto item) =>
    _expandedItem = _expandedItem == item ? null : item;
}