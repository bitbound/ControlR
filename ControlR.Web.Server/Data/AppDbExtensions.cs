using ControlR.Libraries.Shared.Dtos.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;

public static class AppDbExtensions
{
  public static async Task<TEntity> AddOrUpdate<TDto, TEntity>(this AppDb db, TDto dto)
    where TDto : IHasUid
    where TEntity : EntityBase, new()
  {
    var set = db.Set<TEntity>();
    TEntity? entity = null;

    if (dto.Uid != Guid.Empty)
    {
      entity = await set.FirstOrDefaultAsync(x => x.Uid == dto.Uid);
    }

    if (entity is null)
    {
      entity = new TEntity();
      var entry = set.Entry(entity);
      entry.CurrentValues.SetValues(dto);
      entry.State = EntityState.Added;
    }
    else
    {
      var entry = set.Entry(entity);
      entry.CurrentValues.SetValues(dto);
      entry.State = EntityState.Modified;
    }

    await db.SaveChangesAsync();
    return entity;
  }
}
