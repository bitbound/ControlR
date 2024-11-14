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
      entity = await db.Devices
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == dto.Id);
    }

    var entityState = entity is null ? EntityState.Added : EntityState.Modified;
    entity ??= new Device();
    var entry = set.Entry(entity);
    await entry.Reference(x => x.Tenant).LoadAsync();
    await entry.Collection(x => x.Tags!).LoadAsync();
    entry.State = entityState;
    entry.CurrentValues.SetValues(dto);
    
    entity.Drives = dto.Drives.ToList();
    
    // If we're adding a new device, associate it with any tags passed in.
    if (entityState == EntityState.Added && dto.TagIds is { Length: > 0 })
    {
      var tags = await db.Tags
        .Where(x => dto.TagIds.Contains(x.Id))
        .ToListAsync();
      
      entity.Tags = tags;
    }

    await db.SaveChangesAsync();
    return entity;
  }
}
