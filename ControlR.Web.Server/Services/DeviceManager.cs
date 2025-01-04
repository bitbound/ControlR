namespace ControlR.Web.Server.Services;

public interface IDeviceManager
{
  Task<Device> AddOrUpdate(DeviceDto deviceDto, bool addTagIds = false);
  Task<bool> CanInstallAgentOnDevice(AppUser user, Device device);
  Task<Result<Device>> UpdateDevice(DeviceDto deviceDto, bool addTagIds = false);
}

public class DeviceManager(
  AppDb appDb,
  UserManager<AppUser> userManager) : IDeviceManager
{
  private readonly AppDb _appDb = appDb;
  private readonly UserManager<AppUser> _userManager = userManager;

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
      return Result.Fail<Device>("Device not found.");
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
    entry.CurrentValues.SetValuesExcept(
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
}