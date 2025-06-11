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
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.Name, isRelationalDatabase, logger);
          break;
        case nameof(Device.Alias):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.Alias, isRelationalDatabase, logger);
          break;
        case nameof(Device.OsDescription):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.OsDescription, isRelationalDatabase, logger);
          break;
        case nameof(Device.ConnectionId):
          query = query.FilterByStringColumn(filter.Operator, filter.Value, d => d.ConnectionId, isRelationalDatabase, logger);
          break;
        case nameof(Device.IsOnline):
          query = query.FilterByBooleanColumn(filter.Operator, filter.Value, d => d.IsOnline, logger);
          break;
        case nameof(Device.CpuUtilization):
          query = query.FilterByDoubleColumn(filter.Operator, filter.Value, d => d.CpuUtilization, logger);
          break;
        case nameof(Device.UsedMemoryPercent):
          query = query.FilterByDoubleColumn(filter.Operator, filter.Value, d => d.UsedMemoryPercent, logger);
          break;
        case nameof(Device.UsedStoragePercent):
          query = query.FilterByDoubleColumn(filter.Operator, filter.Value, d => d.UsedStoragePercent, logger);
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

  private static Expression<Func<Device, bool>> BuildDoubleExpression(
    Expression<Func<Device, double>> propertySelector,
    Expression<Func<double, bool>> condition)
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
    Expression<Func<Device, bool>> propertySelector,
    ILogger logger)
  {
    // Parse the filter value to boolean
    if (!bool.TryParse(filterValue, out var boolValue))
    {
      // If not a valid boolean, return query unchanged
      return query;
    }    switch (filterOperator)
    {
      // Handle MudBlazor boolean filter operators
      case FilterOperator.Boolean.Is:
        return query.Where(BuildBooleanExpression(propertySelector, boolValue));
      default:
        logger.LogError("Unsupported boolean filter operator: {FilterOperator}", filterOperator);
        return query;
    }
  }

  private static IQueryable<Device> FilterByStringColumn(
    this IQueryable<Device> query,
    string filterOperator,
    string filterValue,
    Expression<Func<Device, string?>> propertySelector,
    bool isRelationalDatabase,
    ILogger logger)
  {    if (isRelationalDatabase)
    {
      switch (filterOperator)
      {
        case FilterOperator.String.Contains:
          return query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"%{filterValue}%")));
        case FilterOperator.String.Empty:
          return query.Where(BuildStringExpression(propertySelector, p => string.IsNullOrWhiteSpace(p)));
        case FilterOperator.String.EndsWith:
          return query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"%{filterValue}")));
        case FilterOperator.String.Equal:
          return query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, filterValue)));
        case FilterOperator.String.NotContains:
          return query.Where(BuildStringExpression(propertySelector, p => !EF.Functions.ILike(p!, $"%{filterValue}%")));
        case FilterOperator.String.NotEmpty:
          return query.Where(BuildStringExpression(propertySelector, p => !string.IsNullOrWhiteSpace(p)));
        case FilterOperator.String.NotEqual:
          return query.Where(BuildStringExpression(propertySelector, p => !EF.Functions.ILike(p!, filterValue)));
        case FilterOperator.String.StartsWith:
          return query.Where(BuildStringExpression(propertySelector, p => EF.Functions.ILike(p!, $"{filterValue}%")));
        default:
          logger.LogError("Unsupported string filter operator for relational database: {FilterOperator}", filterOperator);
          return query;
      }
    }    else
    {
      switch (filterOperator)
      {
        case FilterOperator.String.Contains:
          return query.Where(BuildStringExpression(propertySelector, p => p!.Contains(filterValue, StringComparison.OrdinalIgnoreCase)));
        case FilterOperator.String.Empty:
          return query.Where(BuildStringExpression(propertySelector, p => string.IsNullOrWhiteSpace(p)));
        case FilterOperator.String.EndsWith:
          return query.Where(BuildStringExpression(propertySelector, p => p!.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase)));
        case FilterOperator.String.Equal:
          return query.Where(BuildStringExpression(propertySelector, p => p!.Equals(filterValue, StringComparison.OrdinalIgnoreCase)));
        case FilterOperator.String.NotContains:
          return query.Where(BuildStringExpression(propertySelector, p => !p!.Contains(filterValue, StringComparison.OrdinalIgnoreCase)));
        case FilterOperator.String.NotEmpty:
          return query.Where(BuildStringExpression(propertySelector, p => !string.IsNullOrWhiteSpace(p)));
        case FilterOperator.String.NotEqual:
          return query.Where(BuildStringExpression(propertySelector, p => !p!.Equals(filterValue, StringComparison.OrdinalIgnoreCase)));
        case FilterOperator.String.StartsWith:
          return query.Where(BuildStringExpression(propertySelector, p => p!.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase)));
        default:
          logger.LogError("Unsupported string filter operator for non-relational database: {FilterOperator}", filterOperator);
          return query;
      }
    }
  }
  private static IQueryable<Device> FilterByDoubleColumn(
    this IQueryable<Device> query,
    string filterOperator,
    string filterValue,
    Expression<Func<Device, double>> propertySelector,
    ILogger logger)
  {
    // Parse the filter value to double
    if (!double.TryParse(filterValue, out var doubleValue))
    {
      // If not a valid double, return query unchanged
      return query;
    }    switch (filterOperator)
    {
      // Handle MudBlazor numeric filter operators
      case FilterOperator.Number.Equal:
        return query.Where(BuildDoubleExpression(propertySelector, d => d == doubleValue));
      case FilterOperator.Number.NotEqual:
        return query.Where(BuildDoubleExpression(propertySelector, d => d != doubleValue));
      case FilterOperator.Number.GreaterThan:
        return query.Where(BuildDoubleExpression(propertySelector, d => d > doubleValue));
      case FilterOperator.Number.GreaterThanOrEqual:
        return query.Where(BuildDoubleExpression(propertySelector, d => d >= doubleValue));
      case FilterOperator.Number.LessThan:
        return query.Where(BuildDoubleExpression(propertySelector, d => d < doubleValue));
      case FilterOperator.Number.LessThanOrEqual:
        return query.Where(BuildDoubleExpression(propertySelector, d => d <= doubleValue));
      case FilterOperator.Number.Empty:
        return query.Where(BuildDoubleExpression(propertySelector, d => d == 0));
      case FilterOperator.Number.NotEmpty:
        return query.Where(BuildDoubleExpression(propertySelector, d => d != 0));
      default:
        logger.LogError("Unsupported numeric filter operator: {FilterOperator}", filterOperator);
        return query;
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
