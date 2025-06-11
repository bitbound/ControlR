using MudBlazor;

namespace ControlR.Web.Server.Extensions;

public static class DeviceQueryExtensions
{
  public static IQueryable<Device> FilterBySearchText(
    this IQueryable<Device> query,
    string? searchText,
    bool isRelationalDatabase)
  {
    if (string.IsNullOrWhiteSpace(searchText))
    {
      return query;
    }

    if (isRelationalDatabase)
    {
      return query.Where(d =>
          EF.Functions.ILike(d.Name ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.Alias ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.OsDescription ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(d.ConnectionId ?? "", $"%{searchText}%") ||
          EF.Functions.ILike(string.Join("", d.CurrentUsers) ?? "", $"%{searchText}%"));
    }

    return query.Where(d =>
      d.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
      d.Alias.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
      d.OsDescription.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
      d.ConnectionId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
      string.Join("", d.CurrentUsers).Contains(searchText, StringComparison.OrdinalIgnoreCase));
  }

  public static IQueryable<Device> FilterByOnlineOffline(
    this IQueryable<Device> query,
    bool hideOfflineDevices)
  {
    if (hideOfflineDevices)
    {
      return query.Where(d => d.IsOnline);
    }
    return query;
  }

  public static async Task<IQueryable<Device>?> FilterByTagIds(
    this IQueryable<Device> query,
    List<Guid>? tagIds,
    AppDb appDb)
  {
    if (tagIds is not { Count: > 0 } tags)
    {
      return query;
    }

    // Find devices through the many-to-many relationship
    var deviceIds = await appDb.Devices
        .Where(d => d.Tags!.Any(t => tagIds.Contains(t.Id)))
        .Select(d => d.Id)
        .ToListAsync();

    if (deviceIds.Count > 0)
    {
      return query.Where(d => deviceIds.Contains(d.Id));
    }

    return null;
  }

  public static IQueryable<Device> FilterByColumnFilters(
    this IQueryable<Device> query,
    List<DeviceColumnFilter>? filterDefinitions,
    bool isRelationalDatabase,
    ILogger logger)
  {
    if (filterDefinitions is not { Count: > 0 })
    {
      return query;
    }

    foreach (var filter in filterDefinitions)
    {
      if (!filter.Validate())
      {
        logger.LogError("Invalid column filter definition: {@Filter}", filter);
        continue;
      }

      switch (filter.PropertyName)
      {
        case nameof(Device.Name):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, isRelationalDatabase);
          break;
        case nameof(Device.IsOnline):
          break;
        case nameof(Device.CpuUtilization):
          break;
        case nameof(Device.UsedMemoryPercent):
          break;
        case nameof(Device.UsedStoragePercent):
          break;
        default:
          logger.LogError("Unhandled filter property: {PropertyName}", filter.PropertyName);
          break;
      }
    }
    return query;
  }

  private static IQueryable<Device> FilterByStringColumn(
    this IQueryable<Device> query,
    string filterOperator,
    string filterValue,
    bool isRelationalDatabase)
  {
    if (isRelationalDatabase)
    {
      return filterOperator switch
      {
        FilterOperator.String.Contains =>
          query.Where(d => EF.Functions.ILike(d.Name, $"%{filterValue}%")),
        FilterOperator.String.Empty =>
          query.Where(d => string.IsNullOrWhiteSpace(d.Name)),
        FilterOperator.String.EndsWith =>
          query.Where(d => EF.Functions.ILike(d.Name, $"%{filterValue}")),
        FilterOperator.String.Equal =>
          query.Where(d => EF.Functions.ILike(d.Name, filterValue)),
        FilterOperator.String.NotContains =>
          query.Where(d => !EF.Functions.ILike(d.Name, $"%{filterValue}%")),
        FilterOperator.String.NotEmpty =>
          query.Where(d => !string.IsNullOrWhiteSpace(d.Name)),
        FilterOperator.String.NotEqual =>
          query.Where(d => !EF.Functions.ILike(d.Name, filterValue)),
        FilterOperator.String.StartsWith =>
          query.Where(d => EF.Functions.ILike(d.Name, $"{filterValue}%")),
        _ =>
          throw new ArgumentOutOfRangeException(
            nameof(filterOperator), $"Unsupported filter operator: {filterOperator}"),
      };
    }
    else
    {
      return filterOperator switch
      {
        FilterOperator.String.Contains =>
          query.Where(d => d.Name.Contains(filterValue, StringComparison.OrdinalIgnoreCase)),
        FilterOperator.String.Empty =>
          query.Where(d => string.IsNullOrWhiteSpace(d.Name)),
        FilterOperator.String.EndsWith =>
          query.Where(d => d.Name.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase)),
        FilterOperator.String.Equal =>
          query.Where(d => d.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase)),
        FilterOperator.String.NotContains =>
          query.Where(d => !d.Name.Contains(filterValue, StringComparison.OrdinalIgnoreCase)),
        FilterOperator.String.NotEmpty =>
          query.Where(d => !string.IsNullOrWhiteSpace(d.Name)),
        FilterOperator.String.NotEqual =>
          query.Where(d => !d.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase)),
        FilterOperator.String.StartsWith =>
          query.Where(d => d.Name.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase)),
        _ => throw new ArgumentOutOfRangeException(
              nameof(filterOperator), $"Unsupported filter operator: {filterOperator}"),
      };
    }
  }
}
