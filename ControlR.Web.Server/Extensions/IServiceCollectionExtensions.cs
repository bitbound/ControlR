using ControlR.Web.Server.Mappers;
using ControlR.Web.Server.Services.Repositories;

namespace ControlR.Web.Server.Extensions;

public static class IServiceCollectionExtensions
{
  public static IServiceCollection AddRepository<TDto, TEntity>(
    this IServiceCollection services)
    where TDto : EntityBaseDto, new()
    where TEntity : EntityBase, new()
  {
    return services.AddScoped<IRepository<TDto, TEntity>, Repository<TDto, TEntity>>();
  }

  public static IServiceCollection AddMappers(this IServiceCollection services)
  {
    return services
      .AddScoped<IMapper<Device, DeviceDto>, DeviceToDeviceDtoMapper>()
      .AddScoped<IMapper<DeviceDto, Device>, DeviceDtoToDeviceMapper>()
      .AddScoped<IMapper<DeviceFromAgentDto, Device>, DeviceFromAgentDtoToDeviceMapper>()
      .AddScoped<IMapper<Device, DeviceFromAgentDto>, DeviceToDeviceFromAgentDtoMapper>()
      .AddScoped<IMapper<DeviceDto, DeviceFromAgentDto>, DeviceDtoToDeviceFromAgentDtoMapper>()
      .AddScoped<IMapper<DeviceFromAgentDto, DeviceDto>, DeviceFromAgentDtoToDeviceDtoMapper>();
  }
}
