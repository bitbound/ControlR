using System.Linq.Expressions;

namespace ControlR.Web.Server.Data.Extensions;

public enum SaveChangesResult
{
  Saved,
  ConflictDetected
}

public static class DbContextConflictExtensions
{
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
