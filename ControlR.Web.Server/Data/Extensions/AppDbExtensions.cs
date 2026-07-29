using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ControlR.Web.Server.Data.Extensions;

public static class AppDbExtensions
{
  public static async Task AddOrUpdate<TEntity>(
    this DbContext db,
    TEntity entity,
    Expression<Func<TEntity, bool>> match,
    CancellationToken cancellationToken)
    where TEntity : class
  {
    ArgumentNullException.ThrowIfNull(db);
    ArgumentNullException.ThrowIfNull(entity);
    ArgumentNullException.ThrowIfNull(match);

    var compiled = match.Compile();
    var set = db.Set<TEntity>();

    var existing = set.Local.FirstOrDefault(compiled)
      ?? await set.FirstOrDefaultAsync(match, cancellationToken);

    if (existing is null)
    {
      set.Add(entity);

      var saveResult = await db.SaveChangesOrConfirmConflictAsync(match, cancellationToken);

      if (saveResult == SaveChangesResult.Saved)
        return;

      // Lost the race. Another thread inserted. Detach the failed Add
      // and reload to fall through to the update path below.
      db.Entry(entity).State = EntityState.Detached;
      existing = await set.FirstOrDefaultAsync(match, cancellationToken)
        ?? throw new InvalidOperationException("Expected conflicting entity after SaveChangesOrConfirmConflictAsync.");
    }

    var entry = db.Entry(existing);
    var pkProps = entry.Metadata.FindPrimaryKey()?.Properties ?? [];

    foreach (var prop in entry.Properties)
    {
      if (pkProps.Contains(prop.Metadata))
        continue;

      if (prop.Metadata.ValueGenerated != ValueGenerated.Never)
        continue;

      var propertyInfo = prop.Metadata.PropertyInfo
        ?? throw new InvalidOperationException($"Property {prop.Metadata.Name} has no CLR PropertyInfo.");

      prop.CurrentValue = propertyInfo.GetValue(entity);
    }

    await db.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Calls <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>. If a
  /// <see cref="DbUpdateException"/> is thrown, re-checks the database using
  /// <paramref name="conflictPredicate"/> to confirm the failure was caused by a
  /// unique-constraint violation matching that predicate. Returns
  /// <see cref="SaveChangesResult.ConflictDetected"/> when confirmed; rethrows the
  /// original exception otherwise.
  /// </summary>
  public static async Task<SaveChangesResult> SaveChangesOrConfirmConflictAsync<TEntity>(
    this DbContext db,
    Expression<Func<TEntity, bool>> conflictPredicate,
    CancellationToken cancellationToken = default)
    where TEntity : class
  {
    try
    {
      await db.SaveChangesAsync(cancellationToken);
      return SaveChangesResult.Saved;
    }
    catch (DbUpdateException)
    {
      var isConflict = await db.Set<TEntity>()
        .AsNoTracking()
        .AnyAsync(conflictPredicate, cancellationToken);

      if (!isConflict)
      {
        throw;
      }

      return SaveChangesResult.ConflictDetected;
    }
  }
}

public enum SaveChangesResult
{
  Saved,
  ConflictDetected
}