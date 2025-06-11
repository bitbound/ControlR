using System.Linq.Expressions;
using MudBlazor;

namespace ControlR.Web.Server.Extensions;

public static class DeviceQueryExtensions
{

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
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.Name, isRelationalDatabase);
          break;
        case nameof(Device.Alias):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.Alias, isRelationalDatabase);
          break;
        case nameof(Device.OsDescription):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.OsDescription, isRelationalDatabase);
          break;
        case nameof(Device.ConnectionId):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.ConnectionId, isRelationalDatabase);
          break;
        case nameof(Device.IsOnline):
          query = query.FilterByBooleanColumn(filter.Operator, filter.Value, d => d.IsOnline);
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

  private static Expression<Func<Device, bool>> BuildBooleanExpression(
    Expression<Func<Device, bool>> propertySelector,
    bool expectedValue)
  {
    var parameter = propertySelector.Parameters[0];
    var propertyExpression = propertySelector.Body;

    // Create the comparison expression
    var comparisonExpression = expectedValue
      ? propertyExpression 
      : Expression.Not(propertyExpression); 

    return Expression.Lambda<Func<Device, bool>>(comparisonExpression, parameter);
  }

  private static Expression<Func<Device, bool>> BuildStringExpression(
    Expression<Func<Device, string?>> propertySelector,
    Expression<Func<string?, bool>> condition)
  {
    var parameter = propertySelector.Parameters[0];
    var propertyExpression = propertySelector.Body;
    var conditionBody = condition.Body;

    // Replace the parameter in the condition with the property expression
    var visitor = new ParameterReplacerVisitor(condition.Parameters[0], propertyExpression);
    var replacedCondition = visitor.Visit(conditionBody);

    return Expression.Lambda<Func<Device, bool>>(replacedCondition!, parameter);
  }

  private static IQueryable<Device> FilterByBooleanColumn(
    this IQueryable<Device> query,
    string filterOperator,
    string filterValue,
    Expression<Func<Device, bool>> propertySelector)
  {
    // Parse the filter value to boolean
    if (!bool.TryParse(filterValue, out var boolValue))
    {
      // If not a valid boolean, return query unchanged
      return query;
    }

    return filterOperator switch
    {
      // Handle MudBlazor boolean filter operators
      FilterOperator.Boolean.Is =>
        query.Where(BuildBooleanExpression(propertySelector, boolValue)),
      _ => throw new ArgumentOutOfRangeException(
        nameof(filterOperator), $"Unsupported filter operator: {filterOperator}"),
    };
  }

  private static IQueryable<Device> FilterByStringColumn(
    this IQueryable<Device> query,
    string filterOperator,
    string filterValue,
    Expression<Func<Device, string?>> propertySelector,
    bool isRelationalDatabase)
  {
    if (isRelationalDatabase)
    {
      return filterOperator switch
      {
        FilterOperator.String.Contains =>
          query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"%{filterValue}%"))),
        FilterOperator.String.Empty =>
          query.Where(BuildStringExpression(propertySelector, p => string.IsNullOrWhiteSpace(p))),
        FilterOperator.String.EndsWith =>
          query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"%{filterValue}"))),
        FilterOperator.String.Equal =>
          query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, filterValue))),
        FilterOperator.String.NotContains =>
          query.Where(BuildStringExpression(propertySelector, p => !EF.Functions.ILike(p!, $"%{filterValue}%"))),
        FilterOperator.String.NotEmpty =>
          query.Where(BuildStringExpression(propertySelector, p => !string.IsNullOrWhiteSpace(p))),
        FilterOperator.String.NotEqual =>
          query.Where(BuildStringExpression(propertySelector, p => !EF.Functions.ILike(p!, filterValue))),
        FilterOperator.String.StartsWith =>
          query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"{filterValue}%"))),
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
          query.Where(BuildStringExpression(propertySelector, p => p!.Contains(filterValue, StringComparison.OrdinalIgnoreCase))),
        FilterOperator.String.Empty =>
          query.Where(BuildStringExpression(propertySelector, p => string.IsNullOrWhiteSpace(p))),
        FilterOperator.String.EndsWith =>
          query.Where(BuildStringExpression(propertySelector, p => p!.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase))),
        FilterOperator.String.Equal =>
          query.Where(BuildStringExpression(propertySelector, p => p!.Equals(filterValue, StringComparison.OrdinalIgnoreCase))),
        FilterOperator.String.NotContains =>
          query.Where(BuildStringExpression(propertySelector, p => !p!.Contains(filterValue, StringComparison.OrdinalIgnoreCase))),
        FilterOperator.String.NotEmpty =>
          query.Where(BuildStringExpression(propertySelector, p => !string.IsNullOrWhiteSpace(p))),
        FilterOperator.String.NotEqual =>
          query.Where(BuildStringExpression(propertySelector, p => !p!.Equals(filterValue, StringComparison.OrdinalIgnoreCase))),
        FilterOperator.String.StartsWith =>
          query.Where(BuildStringExpression(propertySelector, p => p!.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase))),
        _ => throw new ArgumentOutOfRangeException(
              nameof(filterOperator), $"Unsupported filter operator: {filterOperator}"),
      };
    }
  }

  private class ParameterReplacerVisitor(ParameterExpression oldParameter, Expression newExpression) : ExpressionVisitor
  {
    private readonly Expression _newExpression = newExpression;
    private readonly ParameterExpression _oldParameter = oldParameter;

    protected override Expression VisitParameter(ParameterExpression node)
    {
      return node == _oldParameter ? _newExpression : base.VisitParameter(node);
    }
  }
}
