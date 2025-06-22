using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

namespace ControlR.Web.Server.Services;

public interface IDeviceManager
{
  Task<Device> AddOrUpdate(DeviceDto deviceDto, bool addTagIds = false);
  Task<bool> CanInstallAgentOnDevice(AppUser user, Device device);
  Task<Result<Device>> UpdateDevice(DeviceDto deviceDto, bool addTagIds = false);
}

public class DeviceManager(
  AppDb appDb,
  UserManager<AppUser> userManager,
  ILogger<DeviceManager> logger) : IDeviceManager
{
  private static readonly BindingFlags _bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
  private static readonly ConcurrentDictionary<Type, ImmutableDictionary<string, PropertyInfo>> _propertiesCache = [];

  private readonly AppDb _appDb = appDb;
  private readonly UserManager<AppUser> _userManager = userManager;
  private readonly ILogger<DeviceManager> _logger = logger;

  public async Task<Device> AddOrUpdate(DeviceDto deviceDto, bool addTagIds = false)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    var entityState = entity is null
      ? EntityState.Added
      : EntityState.Modified;

    entity ??= new Device();

    await UpdateDeviceEntity(entity, deviceDto, entityState, addTagIds);

    return entity;
  }

  public async Task<bool> CanInstallAgentOnDevice(AppUser user, Device device)
  {
    if (user.TenantId != device.TenantId)
    {
      return false;
    }
    return await _userManager.IsInRoleAsync(user, RoleNames.AgentInstaller);
  }

  public async Task<Result<Device>> UpdateDevice(DeviceDto deviceDto, bool addTagIds = false)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    if (entity is null)
    {
      return Result.Fail<Device>("Device does not exist in the database.");
    }

    await UpdateDeviceEntity(entity, deviceDto, EntityState.Modified, addTagIds);

    return Result.Ok(entity);
  }

  private async Task UpdateDeviceEntity(Device entity, DeviceDto deviceDto, EntityState entityState, bool addTagIds)
  {
    var entry = _appDb.Entry(entity);
    await entry.Reference(x => x.Tenant).LoadAsync();
    await entry.Collection(x => x.Tags!).LoadAsync();
    entry.State = entityState;

    SetValuesExcept(
      entry,
      deviceDto,
      nameof(DeviceDto.Alias),
      nameof(DeviceDto.TagIds));

    entity.Drives = [.. deviceDto.Drives];

    if (addTagIds && deviceDto.TagIds is { Length: > 0 } tagIds)
    {
      var tags = await _appDb.Tags
        .Where(x => tagIds.Contains(x.Id))
        .ToListAsync();

      entity.Tags = tags;
    }

    await _appDb.SaveChangesAsync();
  }

  private static void SetValuesExcept<TDto>(
    EntityEntry entry,
    TDto dto,
    params string[] excludeProperties)
    where TDto : notnull
  {
    var dtoProps = _propertiesCache.GetOrAdd(typeof(TDto), t =>
    {
      return t
        .GetProperties(_bindingFlags)
        .ToImmutableDictionary(x => x.Name);
    });

    foreach (var prop in entry.Properties)
    {
      var maxLength = prop.Metadata.GetMaxLength();
      var propName = prop.Metadata.Name;

      if (excludeProperties.Contains(propName))
      {
        continue;
      }

      if (!dtoProps.TryGetValue(propName, out var propInfo))
      {
        continue;
      }

      var dtoValue = propInfo.GetValue(dto);

      if (maxLength.HasValue &&
          maxLength.Value > 0 &&
          prop.Metadata.ClrType == typeof(string) &&
          dtoValue is string dtoString &&
          dtoString.Length > maxLength.Value)
      {
        prop.CurrentValue = dtoString[..maxLength.Value];
      }
      else
      {
        prop.CurrentValue = dtoValue;
      }
    }
  }
}