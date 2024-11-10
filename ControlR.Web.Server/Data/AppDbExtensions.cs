namespace ControlR.Web.Server.Data;

public static class AppDbExtensions
{
  public static async Task<Device> AddOrUpdateDevice(
    this AppDb db, 
    DeviceUpdateRequestDto dto)
  {
    var set = db.Set<Device>();
    Device? entity = null;
    
    if (dto.Id != Guid.Empty)
    {
      entity = await db.Devices.FirstOrDefaultAsync(x => x.Id == dto.Id);
    }

    var entityState = entity is null ? EntityState.Added : EntityState.Modified;
    entity ??= new Device();
    var entry = set.Entry(entity);
    await entry.Reference(x => x.Tenant).LoadAsync();
    await entry.Collection(x => x.Tags!).LoadAsync();
    entry.State = entityState;
    entry.CurrentValues.SetValues(dto);
    entity.Drives = dto.Drives;

    await db.SaveChangesAsync();
    return entity;
  }
}
