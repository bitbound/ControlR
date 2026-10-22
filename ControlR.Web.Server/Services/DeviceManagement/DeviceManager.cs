using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Net.Sockets;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Web.Server.Services.DeviceManagement;

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
  /// <param name="tagIds">Tag ids to set; null = leave unchanged, empty = clear.</param>
  /// <param name="publicKeyBase64">The agent's public key to store; null = leave unchanged.</param>
  /// <param name="customerId">Customer to assign; null = leave unchanged.</param>
  /// <returns>The added or updated <see cref="Device"/> entity.</returns>
  Task<Device> AddOrUpdate(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, IReadOnlyList<Guid>? tagIds = null, string? publicKeyBase64 = null, Guid? customerId = null);

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
  /// <param name="tagIds">Tag ids to set; null = leave unchanged, empty = clear.</param>
  /// <param name="publicKeyBase64">The agent's public key to store; null = leave unchanged.</param>
  /// <param name="customerId">Customer to assign; null = leave unchanged.</param>
  /// <returns>
  ///   A <see cref="Result{Device}"/> containing the updated device if successful,
  ///   or a failure result if the device does not exist.
  /// </returns>
  Task<Result<Device>> UpdateDevice(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, IReadOnlyList<Guid>? tagIds = null, string? publicKeyBase64 = null, Guid? customerId = null);
}

public class DeviceManager(
  AppDb appDb,
  IDeviceConnectionRegistry deviceConnectionRegistry,
  ILogger<DeviceManager> logger) : IDeviceManager
{
  private const BindingFlags PropertiesBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

  private static readonly ConcurrentDictionary<Type, ImmutableDictionary<string, PropertyInfo>> _propertiesCache = [];

  private readonly AppDb _appDb = appDb;
  private readonly IDeviceConnectionRegistry _deviceConnectionRegistry = deviceConnectionRegistry;
  private readonly ILogger<DeviceManager> _logger = logger;

  public async Task<Device> AddOrUpdate(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, IReadOnlyList<Guid>? tagIds = null, string? publicKeyBase64 = null, Guid? customerId = null)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    var entityState = entity is null
      ? EntityState.Added
      : EntityState.Modified;

    entity ??= new Device();

    await UpdateDeviceEntity(entity, deviceDto, context, entityState, tagIds, publicKeyBase64, customerId);

    return entity;
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
    await _deviceConnectionRegistry.Remove(deviceId, entity.ConnectionId);
    entity.ConnectionId = string.Empty; // Clear connection ID when offline

    await _appDb.SaveChangesAsync();

    return Result.Ok(entity);
  }

  public async Task<Result<Device>> UpdateDevice(DeviceUpdateRequestDto deviceDto, DeviceConnectionContext context, IReadOnlyList<Guid>? tagIds = null, string? publicKeyBase64 = null, Guid? customerId = null)
  {
    var entity = await _appDb.Devices
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x => x.Id == deviceDto.Id);

    if (entity is null)
    {
      return Result.Fail<Device>("Device does not exist in the database.");
    }

    if (entity.TenantId != Guid.Empty && entity.TenantId != deviceDto.TenantId)
    {
      return Result.Fail<Device>("Device belongs to a different tenant.");
    }

    await UpdateDeviceEntity(entity, deviceDto, context, EntityState.Modified, tagIds, publicKeyBase64, customerId);

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
        if (dtoValue == null)
        {
          // Can't assign null to a non-nullable value type; skip.
          if (prop.Metadata.ClrType.IsValueType && Nullable.GetUnderlyingType(prop.Metadata.ClrType) == null)
          {
            continue;
          }

          // Can't assign null to a non-nullable reference type; skip.
          if (!prop.Metadata.ClrType.IsValueType && !prop.Metadata.IsNullable)
          {
            continue;
          }
        }
        prop.CurrentValue = dtoValue;
      }
    }
  }

  private async Task UpdateDeviceEntity(
    Device entity,
    DeviceUpdateRequestDto deviceDto,
    DeviceConnectionContext context,
    EntityState entityState,
    IReadOnlyList<Guid>? tagIds = null,
    string? publicKeyBase64 = null,
    Guid? customerId = null)
  {
    var entry = _appDb.Entry(entity);
    await entry.Reference(x => x.Tenant).LoadAsync();
    await entry.Collection(x => x.Tags!).LoadAsync();

    // A device's tenant is immutable once assigned. Reject any attempt to re-home an
    // existing device into a different tenant.
    if (entity.TenantId != Guid.Empty && entity.TenantId != deviceDto.TenantId)
    {
      throw new InvalidOperationException(
        $"Device {deviceDto.Id} belongs to tenant {entity.TenantId} and cannot be moved to tenant {deviceDto.TenantId}.");
    }

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

    if (customerId is not null)
    {
      var customerTenantId = await _appDb.Customers
        .IgnoreQueryFilters()
        .Where(x => x.Id == customerId.Value)
        .Select(x => (Guid?)x.TenantId)
        .FirstOrDefaultAsync() ?? 
          throw new InvalidOperationException($"Customer {customerId} does not exist.");

      if (customerTenantId != entity.TenantId)
      {
        throw new InvalidOperationException(
          $"Customer {customerId} does not belong to the device's tenant {entity.TenantId}.");
      }

      entity.CustomerId = customerId;
    }

    entity.ConnectionId = context.ConnectionId;
    entity.IsOnline = context.IsOnline;
    entity.LastSeen = context.LastSeen;

    if (context.IsOnline && !string.IsNullOrWhiteSpace(context.ConnectionId))
    {
      await _deviceConnectionRegistry.Set(entity.Id, context.ConnectionId);
    }
    else if (!context.IsOnline)
    {
      var trackedConnectionResult = await _deviceConnectionRegistry.GetConnectionId(entity.Id);
      if (trackedConnectionResult.IsSuccess && trackedConnectionResult.Value == context.ConnectionId)
      {
        await _deviceConnectionRegistry.Remove(entity.Id, trackedConnectionResult.Value);
      }
    }

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

    // Adopt the verified public key if one was provided.
    if (!string.IsNullOrEmpty(publicKeyBase64))
    {
      entity.PublicKey = publicKeyBase64;
    }

    await _appDb.SaveChangesAsync();
  }
}