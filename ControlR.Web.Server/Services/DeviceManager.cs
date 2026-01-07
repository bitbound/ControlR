using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Net.Sockets;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Manages device entities in the database.
/// </summary>
public interface IDeviceManager
{
  /// <summary>
  /// Adds a new device or updates an existing device based on the provided DTO.
  /// </summary>
  /// <param name="deviceDto">The data transfer object containing device details.</param>
  /// <param name="context">The context information regarding the device's connection.</param>
  /// <param name="tagIds">
  ///   Optional list of tag IDs to associate with the device.
  ///   If null, tags will not be modified.
  ///   If an empty array is provided, all tags will be removed, if any exist.
  /// </param>
  /// <returns>The added or updated <see cref="Device"/> entity.</returns>
  Task<Device> AddOrUpdate(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, Guid[]? tagIds = null);

  /// <summary>
  /// Determines whether the specified user is authorized to install an agent on the given device.
  /// </summary>
  /// <param name="user">The user attempting to install the agent.</param>
  /// <param name="device">The target device for the agent installation.</param>
  /// <returns>
  ///   <c>true</c> if the user belongs to the same tenant as the device and has the necessary permissions; otherwise, <c>false</c>.
  /// </returns>
  Task<bool> CanInstallAgentOnDevice(AppUser user, Device device);

  /// <summary>
  /// Marks a specific device as offline and updates its last seen timestamp.
  /// </summary>
  /// <param name="deviceId">The unique identifier of the device to mark offline.</param>
  /// <param name="lastSeen">The timestamp indicating when the device was last seen.</param>
  /// <returns>
  ///   A <see cref="Result{Device}"/> containing the updated device if successful,
  ///   or a failure result if the device is not found.
  /// </returns>
  Task<Result<Device>> MarkDeviceOffline(Guid deviceId, DateTimeOffset lastSeen);

  /// <summary>
  /// Updates an existing device with the provided details.
  /// </summary>
  /// <param name="deviceDto">The data transfer object containing updated device details.</param>
  /// <param name="context">The context information regarding the device's connection.</param>
  /// <param name="tagIds">
  ///   Optional list of tag IDs to associate with the device.
  ///   If null, tags will not be modified.
  ///   If an empty array is provided, all tags will be removed.
  /// </param>
  /// <returns>
  ///   A <see cref="Result{Device}"/> containing the updated device if successful,
  ///   or a failure result if the device does not exist.
  /// </returns>
  Task<Result<Device>> UpdateDevice(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, Guid[]? tagIds = null);
}

public class DeviceManager(
  AppDb appDb,
  UserManager<AppUser> userManager,
  ILogger<DeviceManager> logger) : IDeviceManager
{
  private const BindingFlags PropertiesBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

  private static readonly ConcurrentDictionary<Type, ImmutableDictionary<string, PropertyInfo>> _propertiesCache = [];

  private readonly AppDb _appDb = appDb;
  private readonly ILogger<DeviceManager> _logger = logger;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<Device> AddOrUpdate(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, Guid[]? tagIds = null)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    var entityState = entity is null
      ? EntityState.Added
      : EntityState.Modified;

    entity ??= new Device();

    await UpdateDeviceEntity(entity, deviceDto, context, entityState, tagIds);

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

  public async Task<Result<Device>> MarkDeviceOffline(Guid deviceId, DateTimeOffset lastSeen)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceId);

    if (entity is null)
    {
      return Result.Fail<Device>("Device does not exist in the database.");
    }

    entity.IsOnline = false;
    entity.LastSeen = lastSeen;
    entity.ConnectionId = string.Empty; // Clear connection ID when offline

    await _appDb.SaveChangesAsync();

    return Result.Ok(entity);
  }

  public async Task<Result<Device>> UpdateDevice(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, Guid[]? tagIds = null)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    if (entity is null)
    {
      return Result.Fail<Device>("Device does not exist in the database.");
    }

    await UpdateDeviceEntity(entity, deviceDto, context, EntityState.Modified, tagIds);

    return Result.Ok(entity);
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
        .GetProperties(PropertiesBindingFlags)
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

      if (maxLength is > 0 &&
          prop.Metadata.ClrType == typeof(string) &&
          dtoValue is string dtoString &&
          dtoString.Length > maxLength.Value)
      {
        prop.CurrentValue = dtoString[..maxLength.Value];
      }
      else
      {
        // If the value from DTO is null, we need to be careful.
        if (dtoValue == null)
        {
          // If the target property in the Entity is a VALUE type, and it's NOT a Nullable<T> (e.g., int, bool),
          // then we CANNOT assign null. Skip this property.
          if (prop.Metadata.ClrType.IsValueType && Nullable.GetUnderlyingType(prop.Metadata.ClrType) == null)
          {
            continue;
          }

          // If the target property in the Entity is a REFERENCE type, and it's NOT marked as nullable in the model
          // (meaning it corresponds to a non-nullable column in the DB, or a non-nullable C# reference type),
          // then we CANNOT assign null. Skip this property.
          if (!prop.Metadata.ClrType.IsValueType && !prop.Metadata.IsNullable)
          {
            continue;
          }
        }
        // If dtoValue is not null, or if the target property can accept null (either nullable value type or nullable reference type),
        // then it's safe to assign.
        prop.CurrentValue = dtoValue;
      }
    }
  }

  private async Task UpdateDeviceEntity(
    Device entity,
    DeviceUpdateRequestDto deviceDto,
    DeviceConnectionContext context,
    EntityState entityState,
    Guid[]? tagIds = null)
  {
    var entry = _appDb.Entry(entity);
    await entry.Reference(x => x.Tenant).LoadAsync();
    await entry.Collection(x => x.Tags!).LoadAsync();
    entry.State = entityState;

    SetValuesExcept(
      entry,
      deviceDto,
      nameof(DeviceUpdateRequestDto.TenantId)); // TenantId is handled separately

    entity.TenantId = deviceDto.TenantId;
    entity.Drives = [.. deviceDto.Drives];
    if (tagIds is not null)
    {
      entity.Tags = await _appDb.Tags
        .Where(x => tagIds.Contains(x.Id))
        .ToListAsync();
    }

    // Apply server-side determined properties from context
    entity.ConnectionId = context.ConnectionId;
    entity.IsOnline = context.IsOnline;
    entity.LastSeen = context.LastSeen;

    if (context.RemoteIpAddress is not null)
    {
      if (context.RemoteIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
      {
        entity.PublicIpV6 = context.RemoteIpAddress.ToString();
      }
      else if (context.RemoteIpAddress.AddressFamily == AddressFamily.InterNetwork)
      {
        entity.PublicIpV4 = context.RemoteIpAddress.ToString();
      }
      else
      {
        _logger.LogWarning("Unsupported IP address family: {AddressFamily}", context.RemoteIpAddress.AddressFamily);
      }
    }

    await _appDb.SaveChangesAsync();
  }
}