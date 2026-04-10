using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ControlR.Web.Server.Data.Extensions;

public static class UpsertExtensions
{
  private static readonly ConditionalWeakTable<IModel, ConcurrentDictionary<UpsertCacheKey, object>> _metadataCache = new();

  /// <summary>
  /// Inserts a row when it does not exist, or updates it when the primary key already exists.
  /// </summary>
  /// <typeparam name="TEntity">The entity type to upsert.</typeparam>
  /// <param name="db">The database context.</param>
  /// <param name="entity">The entity values to insert or update.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>A task that completes when the upsert statement has finished executing.</returns>
  public static Task UpsertAsync<TEntity>(
      this DbContext db,
      TEntity entity,
      CancellationToken cancellationToken = default)
      where TEntity : class
      => UpsertAsync(db, entity, [], cancellationToken);

  /// <summary>
  /// Inserts a row when it does not exist, or updates it when the specified conflict target already exists.
  /// </summary>
  /// <typeparam name="TEntity">The entity type to upsert.</typeparam>
  /// <param name="db">The database context.</param>
  /// <param name="entity">The entity values to insert or update.</param>
  /// <param name="conflictPropertySelectors">The properties that define the PostgreSQL conflict target.</param>
  /// <param name="cancellationToken">The cancellation token.</param>
  /// <returns>A task that completes when the upsert statement has finished executing.</returns>
  public static Task UpsertAsync<TEntity>(
      this DbContext db,
      TEntity entity,
      IReadOnlyCollection<Expression<Func<TEntity, object?>>> conflictPropertySelectors,
      CancellationToken cancellationToken = default)
      where TEntity : class
  {
    ArgumentNullException.ThrowIfNull(db);
    ArgumentNullException.ThrowIfNull(entity);
    ArgumentNullException.ThrowIfNull(conflictPropertySelectors);

    var conflictPropertyNames = GetConflictPropertyNames(conflictPropertySelectors);
    var modelCache = _metadataCache.GetOrCreateValue(db.Model);
    var meta = (UpsertMetadata<TEntity>)modelCache.GetOrAdd(
        new UpsertCacheKey(typeof(TEntity), string.Join("|", conflictPropertyNames)),
        _ => BuildMetadata<TEntity>(db, conflictPropertyNames));

    var providerKind = GetProviderKind(db);

    if (providerKind == ProviderKind.Npgsql)
    {
      var parameters = meta.BuildParameters(entity);
      return db.Database.ExecuteSqlRawAsync(meta.Sql, parameters, cancellationToken);
    }

    if (providerKind == ProviderKind.InMemory)
    {
      return UpsertWithEfAsync(db, entity, meta, cancellationToken);
    }

    throw new InvalidOperationException(
      $"{nameof(UpsertExtensions)} supports only the Npgsql and InMemory EF Core providers.");
  }

  private static Expression<Func<TEntity, bool>> BuildConflictPredicate<TEntity>(
    TEntity entity,
    IReadOnlyList<IProperty> conflictProperties)
    where TEntity : class
  {
    var parameter = Expression.Parameter(typeof(TEntity), "candidate");
    Expression? body = null;

    foreach (var property in conflictProperties)
    {
      var propertyInfo = property.PropertyInfo
          ?? throw new InvalidOperationException($"Property {property.Name} has no CLR PropertyInfo.");

      var left = Expression.Property(parameter, propertyInfo);
      var value = propertyInfo.GetValue(entity);
      var right = Expression.Constant(value, propertyInfo.PropertyType);
      var equality = Expression.Equal(left, right);

      body = body is null ? equality : Expression.AndAlso(body, equality);
    }

    if (body is null)
    {
      throw new InvalidOperationException("At least one conflict property is required to build an upsert predicate.");
    }

    return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
  }

  private static UpsertMetadata<TEntity> BuildMetadata<TEntity>(
      DbContext db,
      IReadOnlyList<string> conflictPropertyNames)
    where TEntity : class
  {
    var entityType = db.Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"No entity type mapped for {typeof(TEntity).Name}");

    var tableName = entityType.GetTableName()
        ?? throw new InvalidOperationException($"Entity {entityType.Name} is not mapped to a table.");

    var schema = entityType.GetSchema();
    var storeObject = StoreObjectIdentifier.Table(tableName, schema);

    var primaryKey = entityType.FindPrimaryKey()
        ?? throw new InvalidOperationException($"Entity {entityType.Name} has no primary key.");

    var conflictProps = ResolveConflictProperties(entityType, storeObject, primaryKey.Properties, conflictPropertyNames);
    var protectedProps = primaryKey.Properties
        .Concat(conflictProps)
        .ToHashSet();

    // Columns participating in INSERT
    var insertProps = entityType
        .GetProperties()
        .Where(p => !p.IsShadowProperty())
        .Where(p => p.GetColumnName(storeObject) is not null)
        .Where(p => p.GetBeforeSaveBehavior() == PropertySaveBehavior.Save)
        .ToArray();

    // Columns participating in UPDATE (no keys/conflict columns, no store-generated)
    var updateProps = insertProps
        .Where(p => !protectedProps.Contains(p))
        .Where(p => p.GetAfterSaveBehavior() == PropertySaveBehavior.Save)
        .ToArray();

    var columnNames = insertProps
        .Select(p => GetQuotedColumnName(p, storeObject))
        .ToArray();

    var conflictColumnNames = conflictProps
        .Select(p => GetQuotedColumnName(p, storeObject))
        .ToArray();

    var updateAssignments = updateProps
        .Select(p =>
        {
          var col = GetQuotedColumnName(p, storeObject);
          return $"{col} = EXCLUDED.{col}";
        })
        .ToArray();

    var insertList = string.Join(", ", columnNames);
    var valuesList = string.Join(", ", Enumerable.Range(0, insertProps.Length).Select(i => $"{{{i}}}"));
    var conflictList = string.Join(", ", conflictColumnNames);
    var updateList = updateAssignments.Length > 0
        ? string.Join(", ", updateAssignments)
        : string.Join(", ", conflictColumnNames.Select(c => $"{c} = {c}")); // no-op but valid SQL

    var fullTableName = schema is null
        ? QuoteIdentifier(tableName)
        : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";

    var sql = $"""
      INSERT INTO {fullTableName} ({insertList})
      VALUES ({valuesList})
      ON CONFLICT ({conflictList})
      DO UPDATE SET {updateList};
      """;

    var parameterBuilder = BuildParameterDelegate<TEntity>(insertProps);

    return new UpsertMetadata<TEntity>(sql, parameterBuilder, conflictProps, updateProps);
  }

  private static Func<TEntity, object[]> BuildParameterDelegate<TEntity>(IReadOnlyList<IProperty> props)
  {
    var entityParam = Expression.Parameter(typeof(TEntity), "e");

    var bindings = props
        .Select(p =>
        {
          var propInfo = p.PropertyInfo
              ?? throw new InvalidOperationException($"Property {p.Name} has no CLR PropertyInfo.");

          Expression access = Expression.Property(entityParam, propInfo);

          if (access.Type.IsValueType)
            access = Expression.Convert(access, typeof(object));

          return access;
        })
        .ToArray();

    var newArray = Expression.NewArrayInit(typeof(object), bindings);
    var lambda = Expression.Lambda<Func<TEntity, object[]>>(newArray, entityParam);
    return lambda.Compile();
  }

  private static bool ConflictPropertiesMatch<TEntity>(
    TEntity left,
    TEntity right,
    IReadOnlyList<IProperty> conflictProperties)
    where TEntity : class
  {
    foreach (var property in conflictProperties)
    {
      var propertyInfo = property.PropertyInfo
          ?? throw new InvalidOperationException($"Property {property.Name} has no CLR PropertyInfo.");

      var leftValue = propertyInfo.GetValue(left);
      var rightValue = propertyInfo.GetValue(right);
      if (!Equals(leftValue, rightValue))
      {
        return false;
      }
    }

    return true;
  }

  private static async Task<TEntity?> FindExistingEntityAsync<TEntity>(
    DbContext db,
    TEntity entity,
    IReadOnlyList<IProperty> conflictProperties,
    CancellationToken cancellationToken)
    where TEntity : class
  {
    var predicate = BuildConflictPredicate(entity, conflictProperties);
    return await db.Set<TEntity>()
      .FirstOrDefaultAsync(predicate, cancellationToken);
  }

  private static TEntity? FindTrackedEntity<TEntity>(
    DbContext db,
    TEntity entity,
    IReadOnlyList<IProperty> conflictProperties)
    where TEntity : class
  {
    return db.Set<TEntity>()
      .Local
      .FirstOrDefault(candidate => ConflictPropertiesMatch(candidate, entity, conflictProperties));
  }

  private static string[] GetConflictPropertyNames<TEntity>(
      IReadOnlyCollection<Expression<Func<TEntity, object?>>> conflictPropertySelectors)
  {
    if (conflictPropertySelectors.Count == 0)
    {
      return [];
    }

    return conflictPropertySelectors
        .Select(selector => selector ?? throw new ArgumentException("Conflict property selectors cannot contain null entries.", nameof(conflictPropertySelectors)))
        .Select(GetPropertyName)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
  }

  private static string GetPropertyName<TEntity>(Expression<Func<TEntity, object?>> selector)
  {
    var expression = selector.Body is UnaryExpression
    {
      NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
      Operand: var operand
    }
        ? operand
        : selector.Body;

    if (expression is not MemberExpression
      {
        Member: PropertyInfo property,
        Expression: ParameterExpression
      })
    {
      throw new ArgumentException(
          $"Conflict selector '{selector}' must target a mapped property.",
          nameof(selector));
    }

    return property.Name;
  }

  private static ProviderKind GetProviderKind(DbContext db)
  {
    return db.Database.ProviderName switch
    {
      "Npgsql.EntityFrameworkCore.PostgreSQL" => ProviderKind.Npgsql,
      "Microsoft.EntityFrameworkCore.InMemory" => ProviderKind.InMemory,
      _ => ProviderKind.Unsupported
    };
  }

  private static string GetQuotedColumnName(IProperty property, StoreObjectIdentifier storeObject)
          => QuoteIdentifier(property.GetColumnName(storeObject)
                  ?? throw new InvalidOperationException($"Property {property.Name} is not mapped to a table column."));

  private static string QuoteIdentifier(string identifier)
      => $"\"{identifier.Replace("\"", "\"\"")}\"";

  private static IReadOnlyList<IProperty> ResolveConflictProperties(
      IEntityType entityType,
      StoreObjectIdentifier storeObject,
      IReadOnlyList<IProperty> primaryKeyProperties,
      IReadOnlyList<string> conflictPropertyNames)
  {
    if (conflictPropertyNames.Count == 0)
    {
      return primaryKeyProperties;
    }

    return conflictPropertyNames
        .Select(propertyName => entityType.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"Property {propertyName} is not mapped on {entityType.Name}."))
        .Select(property => property.GetColumnName(storeObject) is not null
                ? property
                : throw new InvalidOperationException($"Property {property.Name} is not mapped to a table column on {entityType.Name}."))
        .ToArray();
  }

  private static async Task UpsertWithEfAsync<TEntity>(
    DbContext db,
    TEntity entity,
    UpsertMetadata<TEntity> meta,
    CancellationToken cancellationToken)
    where TEntity : class
  {
    var existingEntity = FindTrackedEntity(db, entity, meta.ConflictProperties)
      ?? await FindExistingEntityAsync(db, entity, meta.ConflictProperties, cancellationToken);

    if (existingEntity is null)
    {
      _ = db.Set<TEntity>().Add(entity);
      await db.SaveChangesAsync(cancellationToken);
      return;
    }

    foreach (var property in meta.UpdateProperties)
    {
      var propertyInfo = property.PropertyInfo
          ?? throw new InvalidOperationException($"Property {property.Name} has no CLR PropertyInfo.");

      db.Entry(existingEntity).Property(property.Name).CurrentValue = propertyInfo.GetValue(entity);
    }

    await db.SaveChangesAsync(cancellationToken);
  }

  private enum ProviderKind
  {
    Unsupported,
    Npgsql,
    InMemory
  }

  private sealed record UpsertCacheKey(Type EntityType, string ConflictKey);
  private sealed record UpsertMetadata<TEntity>(
    string Sql,
    Func<TEntity, object[]> BuildParameters,
    IReadOnlyList<IProperty> ConflictProperties,
    IReadOnlyList<IProperty> UpdateProperties);
}
