using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization.Capabilities;

/// <summary>
/// Answers authorization questions about a <see cref="Device"/>, including prospective deployment
/// targets. Kept separate from device data mutation (<see cref="DeviceManagement.DeviceManager"/>)
/// so that all device-scoped permission decisions live in one place instead of accumulating as
/// methods on a data manager.
/// </summary>
public interface IDeviceAuthorizationService
{
  /// <summary>
  /// Determines whether the specified user is authorized to assign tags on the given device.
  /// </summary>
  Task<bool> CanAssignTagOnDevice(AppUser user, Device device);

  /// <summary>
  /// Determines whether the specified service account is authorized to assign tags on the given device.
  /// </summary>
  Task<bool> CanAssignTagOnDevice(ServiceAccount serviceAccount, Device device);

  /// <summary>
  /// Evaluates the device-scoped <c>DeviceTagsWrite</c> decision for a prospective deployment
  /// target. If <paramref name="deviceId"/> identifies an existing device in the tenant, its
  /// real group memberships and customer are used; otherwise it is treated as a new device
  /// (optionally bound to <paramref name="customerId"/>) with no group memberships. This
  /// mirrors the enforcement applied at agent registration so the deploy UI only offers tag
  /// selection when the eventual install could succeed.
  /// </summary>
  Task<bool> CanAssignTagOnProspectiveDevice(
    PrincipalDescriptor principal,
    Guid? deviceId,
    Guid? customerId,
    Guid tenantId,
    CancellationToken cancellationToken);

  /// <summary>
  /// Determines whether the specified user is authorized to install an agent on the given device.
  /// </summary>
  /// <param name="user">The user attempting to install the agent.</param>
  /// <param name="device">The target device for the agent installation.</param>
  /// <returns>
  ///   <c>true</c> if the user belongs to the same tenant as the device and has the necessary
  ///   permissions; otherwise, <c>false</c>.
  /// </returns>
  Task<bool> CanInstallAgentOnDevice(AppUser user, Device device);

  /// <summary>
  /// Determines whether the specified service account is authorized to install an agent on the given device.
  /// </summary>
  Task<bool> CanInstallAgentOnDevice(ServiceAccount serviceAccount, Device device);
}

/// <inheritdoc cref="IDeviceAuthorizationService"/>
public sealed class DeviceAuthorizationService(
  AppDb appDb,
  IResourceDescriptorFactory resourceFactory,
  IPermissionEvaluator permissionEvaluator) : IDeviceAuthorizationService
{
  private readonly AppDb _appDb = appDb;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;

  public async Task<bool> CanAssignTagOnDevice(AppUser user, Device device)
    => await CanAssignTagOnDevice(
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, AuthMethod: "cookie"),
      device);

  public async Task<bool> CanAssignTagOnDevice(ServiceAccount serviceAccount, Device device)
  {
    var principal = CreateServiceAccountPrincipal(serviceAccount);
    return await CanAssignTagOnDevice(principal, device);
  }

  public async Task<bool> CanAssignTagOnProspectiveDevice(
    PrincipalDescriptor principal,
    Guid? deviceId,
    Guid? customerId,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    // If the target ids resolve to an existing device or customer outside the caller's tenant,
    // fail closed. This guards the capability lookup against cross-tenant disclosure.
    Device? existingDevice = null;
    if (deviceId.HasValue)
    {
      existingDevice = await _appDb.Devices
        .IgnoreQueryFilters()
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == deviceId.Value, cancellationToken);
      if (existingDevice is not null && existingDevice.TenantId != tenantId)
      {
        return false;
      }
    }

    if (customerId.HasValue)
    {
      var customerBelongsToTenant = await _appDb.Customers
        .IgnoreQueryFilters()
        .AsNoTracking()
        .AnyAsync(x => x.Id == customerId.Value && x.TenantId == tenantId, cancellationToken);
      if (!customerBelongsToTenant)
      {
        return false;
      }
    }

    // For an existing target, the resource factory reflects its real group memberships (queried
    // from the database when the navigation is not populated). For a new target, no memberships
    // exist, so the transient device carries none.
    var prospectiveDevice = new Device
    {
      Id = deviceId ?? Guid.Empty,
      TenantId = tenantId,
      CustomerId = customerId ?? existingDevice?.CustomerId,
      DeviceGroupMembers = existingDevice is null ? [] : null
    };

    var resource = await _resourceFactory.CreateDevice(prospectiveDevice, cancellationToken);
    var decision = await _permissionEvaluator.Evaluate(
      principal,
      PermissionNames.DeviceTagsWrite,
      resource,
      cancellationToken);
    return decision.Allowed;
  }

  public async Task<bool> CanInstallAgentOnDevice(AppUser user, Device device)
    => await CanInstallAgentOnDevice(
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, AuthMethod: "cookie"),
      device);

  public async Task<bool> CanInstallAgentOnDevice(ServiceAccount serviceAccount, Device device)
  {
    var principal = CreateServiceAccountPrincipal(serviceAccount);
    return await CanInstallAgentOnDevice(principal, device);
  }

  private static PrincipalDescriptor CreateServiceAccountPrincipal(ServiceAccount serviceAccount)
    => new(
      serviceAccount.Kind == ServiceAccountKind.Server
        ? PrincipalType.ServerServiceAccount
        : PrincipalType.TenantServiceAccount,
      serviceAccount.Id,
      serviceAccount.TenantId,
      AuthMethod: PrincipalClaimValues.ServiceAccountCredentialMethod);

  private async Task<bool> CanAssignTagOnDevice(PrincipalDescriptor principal, Device device)
    => await HasDevicePermission(principal, device, PermissionNames.DeviceTagsWrite);

  /// <summary>
  /// Evaluates <see cref="PermissionNames.AgentInstall"/> at device scope so device-scoped
  /// denies on <paramref name="device"/> are honored regardless of broader tenant rights.
  /// </summary>
  private async Task<bool> CanInstallAgentOnDevice(PrincipalDescriptor principal, Device device)
    => await HasDevicePermission(principal, device, PermissionNames.AgentInstall);

  private async Task<bool> HasDevicePermission(PrincipalDescriptor principal, Device device, string permissionName)
  {
    var resource = await _resourceFactory.CreateDevice(device);

    var result = await _permissionEvaluator.Evaluate(
      principal,
      permissionName,
      resource,
      CancellationToken.None);
    return result.Allowed;
  }
}
