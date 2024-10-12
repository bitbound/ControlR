using ControlR.Libraries.Shared.Dtos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace ControlR.Web.Server.Data;

public static class AppDbExtensions
{
  public static async Task<TEntity> AddOrUpdate<TDto, TEntity>(
    this AppDb db, 
    TDto dto,
    IEnumerable<Expression<Func<TEntity, object?>>>? navigations = null)
    where TDto : IHasUid
    where TEntity : EntityBase, new()
  {
    var set = db.Set<TEntity>();
    TEntity? entity = null;

    if (dto.Uid != Guid.Empty)
    {
      entity = await set.FirstOrDefaultAsync(x => x.Uid == dto.Uid);
    }

    entity ??= new TEntity();
    var entry = set.Entry(entity);
    entry.CurrentValues.SetValues(dto);
    entry.State = entity.Id == 0 ? 
      EntityState.Added : 
      EntityState.Modified;

    if (navigations is not null)
    {
      foreach (var navigation in navigations)
      {
        await entry.Reference(navigation).LoadAsync();
      }
    }

    await db.SaveChangesAsync();
    return entity;
  }
}
