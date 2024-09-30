using ControlR.Web.Server.Mappers;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Services.Repositories;

public interface IRepository<TDto, TEntity>
  where TDto : EntityBaseDto, new()
  where TEntity : EntityBase, new()
{
  Task<TEntity> AddOrUpdate(TDto dto);

  IQueryable<TEntity> AsQueryable();

  Task<bool> Delete(int id);

  Task<List<TEntity>> GetAll(
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<TEntity?> GetById(
        int id,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<TEntity?> GetByUid(
    Guid uid,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);
  Task<List<TEntity>> GetWhere(
    Func<IQueryable<TEntity>, IQueryable<TEntity>> whereFilter,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<TEntity?> UpdatePartial(object partialDto, int id);
  Task<TEntity?> UpdatePartial(object partialDto, Guid uid);
}

public class Repository<TDto, TEntity>(
  AppDb appDb,
  IMapper<TDto, TEntity> entityMapper) : IRepository<TDto, TEntity>
  where TDto : EntityBaseDto, new()
  where TEntity : EntityBase, new()
{
  private readonly AppDb _appDb = appDb;

  public async Task<TEntity> AddOrUpdate(TDto dto)
  {
    var set = _appDb.Set<TEntity>();
    var entity = await set.FirstOrDefaultAsync(x => x.Uid == dto.Uid);

    if (entity is null)
    {
      entity = entityMapper.Map(dto);
      await set.AddAsync(entity);
    }
    else
    {
      entityMapper.Map(dto, entity);
    }

    await _appDb.SaveChangesAsync();
    return entity;
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

  public async Task<List<TEntity>> GetAll(
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder(query);
    }

    return await query.ToListAsync();
  }

  public async Task<TEntity?> GetById(
    int id,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var entity = await query.FirstOrDefaultAsync(x => x.Id == id);
    return entity;
  }

  public async Task<TEntity?> GetByUid(
    Guid uid,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder.Invoke(query);
    }

    var entity = await query.FirstOrDefaultAsync(x => x.Uid == uid);
    return entity;
  }

  public async Task<List<TEntity>> GetWhere(
    Func<IQueryable<TEntity>, IQueryable<TEntity>> whereFilter,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null)
  {
    var query = _appDb.Set<TEntity>().AsQueryable();
    if (includeBuilder is not null)
    {
      query = includeBuilder(query);
    }

    query = whereFilter(query);
    var entities = await query.ToListAsync();
    return entities;
  }

  public async Task<TEntity?> UpdatePartial(object partialDto, int id)
  {
    var set = _appDb.Set<TEntity>();
    var entity = await set.FindAsync(id);

    if (entity is null)
    {
      return null;
    }

    _appDb.Entry(entity).CurrentValues.SetValues(partialDto);
    await _appDb.SaveChangesAsync();
    return entity;
  }

  public async Task<TEntity?> UpdatePartial(object partialDto, Guid uid)
  {
    var set = _appDb.Set<TEntity>();
    var entity = await set.FirstOrDefaultAsync(x => x.Uid == uid);

    if (entity is null)
    {
      return null;
    }

    _appDb.Entry(entity).CurrentValues.SetValues(partialDto);
    await _appDb.SaveChangesAsync();
    return entity;
  }
}

