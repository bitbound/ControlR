using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Services.Customers;

/// <summary>
/// Manages customers with tenant-scoped name uniqueness. Devices reference a customer
/// through a nullable foreign key; deleting a customer unassigns its devices.
/// </summary>
public interface ICustomerManager
{
  Task<HttpResult> AssignDevices(
    Guid customerId, IReadOnlyList<Guid> deviceIds, IReadOnlyList<Guid>? removeDeviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.CustomerDto>> Create(
    string name, string? description, string? notes, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult> Delete(
    Guid customerId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.CustomerDto>> Get(
    Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<InternalDtos.CustomerDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default);
  Task<HttpResult<InternalDtos.CustomerDto>> Update(
    Guid customerId, string name, string? description, string? notes, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default);
}

public class CustomerManager(AppDb appDb, IAuthorizationChangeLogFactory changeLogFactory) : ICustomerManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;

  public async Task<HttpResult> AssignDevices(
    Guid customerId, IReadOnlyList<Guid> deviceIds, IReadOnlyList<Guid>? removeDeviceIds, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var customerExists = await _appDb.Customers
      .AnyAsync(x => x.Id == customerId && x.TenantId == tenantId, cancellationToken);

    if (!customerExists)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Customer not found.");
    }

    var totalAffected = 0;

    if (deviceIds.Count > 0)
    {
      var distinctDeviceIds = deviceIds.Distinct().ToList();
      var devices = await _appDb.Devices
        .Where(x => x.TenantId == tenantId && distinctDeviceIds.Contains(x.Id))
        .ToListAsync(cancellationToken);

      if (devices.Count != distinctDeviceIds.Count)
      {
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, "One or more devices were not found in this tenant.");
      }

      foreach (var device in devices)
      {
        device.CustomerId = customerId;
      }

      totalAffected += devices.Count;
    }

    if (removeDeviceIds is { Count: > 0 })
    {
      var removals = await _appDb.Devices
        .Where(x => x.TenantId == tenantId && x.CustomerId == customerId && removeDeviceIds.Contains(x.Id))
        .ToListAsync(cancellationToken);

      foreach (var device in removals)
      {
        device.CustomerId = null;
      }

      totalAffected += removals.Count;
    }

    if (totalAffected == 0)
    {
      return HttpResult.Ok();
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.CustomerDevicesAssigned,
      actor,
      AuthorizationChangeLogTargetTypes.Customer,
      customerId,
      tenantId,
      after: new CustomerDeviceAssignmentChange(totalAffected)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.CustomerDto>> Create(
    string name, string? description, string? notes, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var nameConflict = await _appDb.Customers
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.Conflict, "A customer with that name already exists.");
    }

    var customer = new Customer
    {
      Name = name,
      Description = description,
      Notes = notes,
      TenantId = tenantId
    };

    _appDb.Customers.Add(customer);

    var saveResult = await _appDb.SaveChangesOrConfirmConflictAsync<Customer>(
      x => x.TenantId == tenantId && x.Name == name,
      cancellationToken);

    if (saveResult == SaveChangesResult.ConflictDetected)
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.Conflict, "A customer with that name already exists.");
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.CustomerCreated,
      actor,
      AuthorizationChangeLogTargetTypes.Customer,
      customer.Id,
      tenantId,
      after: new CustomerSnapshot(name, description, notes)));

    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(MapToDto(customer, 0));
  }

  public async Task<HttpResult> Delete(
    Guid customerId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    var customer = await _appDb.Customers
      .FirstOrDefaultAsync(x => x.Id == customerId && x.TenantId == tenantId, cancellationToken);

    if (customer is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Customer not found.");
    }

    var devices = await _appDb.Devices
      .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
      .ToListAsync(cancellationToken);

    foreach (var device in devices)
    {
      device.CustomerId = null;
    }

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.CustomerDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.Customer,
      customerId,
      tenantId,
      before: new CustomerSnapshot(customer.Name, customer.Description, customer.Notes)));

    _appDb.Customers.Remove(customer);
    await _appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<InternalDtos.CustomerDto>> Get(
    Guid customerId, Guid tenantId, CancellationToken cancellationToken = default)
  {
    var customer = await _appDb.Customers
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == customerId && x.TenantId == tenantId, cancellationToken);

    if (customer is null)
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.NotFound, "Customer not found.");
    }

    var deviceCount = await _appDb.Devices
      .CountAsync(x => x.TenantId == tenantId && x.CustomerId == customerId, cancellationToken);

    return HttpResult.Ok(MapToDto(customer, deviceCount));
  }

  public async Task<IReadOnlyList<InternalDtos.CustomerDto>> GetAll(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var customers = await _appDb.Customers
      .Where(x => x.TenantId == tenantId)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    var customerIds = customers.Select(x => x.Id).ToList();

    var deviceCounts = await _appDb.Devices
      .Where(x => x.TenantId == tenantId && x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value))
      .GroupBy(x => x.CustomerId!.Value)
      .Select(g => new { CustomerId = g.Key, Count = g.Count() })
      .ToDictionaryAsync(x => x.CustomerId, x => x.Count, cancellationToken);

    return [.. customers.Select(c => MapToDto(c, deviceCounts.GetValueOrDefault(c.Id, 0)))];
  }

  public async Task<HttpResult<InternalDtos.CustomerDto>> Update(
    Guid customerId, string name, string? description, string? notes, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var customer = await _appDb.Customers
      .FirstOrDefaultAsync(x => x.Id == customerId && x.TenantId == tenantId, cancellationToken);

    if (customer is null)
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.NotFound, "Customer not found.");
    }

    var nameConflict = await _appDb.Customers
      .AnyAsync(x => x.TenantId == tenantId && x.Name == name && x.Id != customerId, cancellationToken);

    if (nameConflict)
    {
      return HttpResult.Fail<InternalDtos.CustomerDto>(HttpResultErrorCode.Conflict, "A customer with that name already exists.");
    }

    var before = new CustomerSnapshot(customer.Name, customer.Description, customer.Notes);

    customer.Name = name;
    customer.Description = description;
    customer.Notes = notes;

    _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.CustomerUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.Customer,
      customerId,
      tenantId,
      before: before,
      after: new CustomerSnapshot(name, description, notes)));

    await _appDb.SaveChangesAsync(cancellationToken);

    var deviceCount = await _appDb.Devices
      .CountAsync(x => x.TenantId == tenantId && x.CustomerId == customerId, cancellationToken);

    return HttpResult.Ok(MapToDto(customer, deviceCount));
  }

  private static InternalDtos.CustomerDto MapToDto(Customer customer, int deviceCount)
  {
    return new InternalDtos.CustomerDto(
      customer.Id, customer.Name, customer.Description, customer.Notes, customer.CreatedAt, deviceCount);
  }
}
