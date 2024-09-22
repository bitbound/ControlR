using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Services.Repositories;

public interface IRepository<TDto, TEntity> 
  where TDto : EntityDtoBase
  where TEntity : EntityBase
{
  IQueryable<TEntity> AsQueryable();

  Task<TDto?> GetById(
    int id,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<TDto?> GetByUid(
    Guid uid,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<TEntity?> AddOrUpdate(TDto dto);

  Task<List<TDto>> GetAll(
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<List<TDto>> GetWhere(
    Func<IQueryable<TEntity>, IQueryable<TEntity>> whereFilter,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeBuilder = null);

  Task<bool> Delete(int id);
}
