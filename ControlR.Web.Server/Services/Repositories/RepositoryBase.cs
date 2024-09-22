using ControlR.Web.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Services.Repositories;

public abstract class RepositoryBase<TDto, TEntity>(AppDb appDb) : IRepository<TDto, TEntity>
  where TDto : EntityDtoBase
  where TEntity : EntityBase
{
  private readonly AppDb _appDb = appDb;

  public async Task<TEntity?> AddOrUpdate(TDto dto)
  {
    var set = _appDb.Set<TEntity>();
    var existing = await set.FirstOrDefaultAsync(x => x.Uid == dto.Uid);
    existing = MapToEntity(dto, existing);
    if (existing.Id == 0)
    {
      await set.AddAsync(existing);
    }

    await _appDb.SaveChangesAsync();
    return existing;
  }

  public IQueryable<TEntity> AsQueryable()
  {
    return _appDb.Set<TEntity>().AsQueryable();
  }

  public async Task<bool> Delete(int id)
  {
    var set = _appDb.Set<TEntity>();
    var entity = await set.FindAsync(id);
    if (entity is null)
    {
      return false;
    }

    set.Remove(entity);
    await _appDb.SaveChangesAsync();
    return true;
  }

  public async Task<List<TDto>> GetAll(
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder(query);
    }

    var entities = await query.ToArrayAsync();

    return entities.Select(MapToDto).ToList();
  }

  public async Task<TDto?> GetById(
    int id,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var entity = await query.FirstOrDefaultAsync(x => x.Id == id);
    return entity is not null
      ? MapToDto(entity)
      : null;
  }

  public async Task<TDto?> GetByUid(
    Guid uid,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var entity = await query.FirstOrDefaultAsync(x => x.Uid == uid);
    return entity is not null
      ? MapToDto(entity)
      : null;
  }

  public async Task<List<TDto>> GetWhere(
    Func<IQueryable<TEntity>, IQueryable<TEntity>> whereFilter,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder(query);
    }

    query = whereFilter(query);
    var entities = await query.ToArrayAsync();
    return entities.Select(MapToDto).ToList();
  }

  protected abstract TDto MapToDto(TEntity entity);
  protected abstract TEntity MapToEntity(TDto dto, TEntity? existing);
}