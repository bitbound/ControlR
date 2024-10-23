using ControlR.Libraries.Shared.Dtos.Interfaces;
using ControlR.Web.Server.Data.Entities.Bases;
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
    where TDto : IHasPrimaryKey
    where TEntity : EntityBase, new()
  {
    var set = db.Set<TEntity>();
    TEntity? entity = null;
    
    if (dto.Id != Guid.Empty)
    {
      entity = await set.FirstOrDefaultAsync(x => x.Id == dto.Id);
    }

    var entityState = entity is null ? EntityState.Added : EntityState.Modified;
    entity ??= new TEntity();
    var entry = set.Entry(entity);
    entry.CurrentValues.SetValues(dto);
    entry.State = entityState;

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
