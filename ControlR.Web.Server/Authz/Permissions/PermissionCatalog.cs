using System.Collections.Frozen;
using System.Collections.Immutable;

namespace ControlR.Web.Server.Authz.Permissions;

public static class PermissionCatalog
{
  private static readonly FrozenDictionary<string, PermissionMetadata> _permissions = BuildCatalog();

  public static IReadOnlyDictionary<string, PermissionMetadata> All => _permissions;

  /// <summary>
  /// By-name lookup of <see cref="PermissionMetadata.AllowsTenantScope"/>. Unknown permissions return false.
  /// </summary>
  public static bool AllowsTenantScope(string permissionName) =>
    Get(permissionName)?.AllowsTenantScope ?? false;

  public static bool Exists(string permissionName) => _permissions.ContainsKey(permissionName);

  public static PermissionMetadata? Get(string permissionName) =>
    _permissions.GetValueOrDefault(permissionName);

  /// <summary>
  /// Returns the broadest legal scope for a permission, per the scope-breadth ordering
  /// (Device, DeviceGroup, CustomerTenant, Tenant, then Server). Note that for device
  /// permissions this includes <see cref="PermissionScopeKind.Server"/>, which is legal
  /// only for cross-tenant server principals. Use <see cref="GetBroadestTenantLegalScope"/>
  /// when seeding presets or selecting a default tenant-bound scope.
  /// </summary>
  public static PermissionScopeKind? GetBroadestLegalScope(string permissionName)
  {
    var kinds = AllowedKinds(permissionName);
    if (kinds is null || kinds.Value.IsDefaultOrEmpty)
    {
      return null;
    }

    return PermissionScopeKinds.GetBroadestLegalScope(kinds);
  }

  /// <summary>
  /// Returns the broadest legal scope for a permission that remains within a tenant boundary
  /// (i.e. excludes <see cref="PermissionScopeKind.Server"/>). Used by preset seeding so that
  /// presets — which always target a specific tenant — never produce server-scoped grants (which
  /// would be cross-tenant by definition). If the permission's only legal scope is Server, falls
  /// back to <see cref="GetBroadestLegalScope"/>.
  /// </summary>
  public static PermissionScopeKind? GetBroadestTenantLegalScope(string permissionName)
  {
    var kinds = AllowedKinds(permissionName);
    if (kinds is null || kinds.Value.IsDefaultOrEmpty)
    {
      return null;
    }

    return PermissionScopeKinds.GetBroadestTenantLegalScope(kinds.Value);
  }

  private static ImmutableArray<PermissionScopeKind>? AllowedKinds(string permissionName) =>
    _permissions.GetValueOrDefault(permissionName)?.AllowedScopeKinds;

  private static FrozenDictionary<string, PermissionMetadata> BuildCatalog()
  {
    var catalog = new Dictionary<string, PermissionMetadata>();

    void Add(string name, string categoryLabel, string displayName, string description, ImmutableArray<PermissionScopeKind> scopeKinds, bool selfRemovable = true)
    {
      catalog[name] = new PermissionMetadata(name, categoryLabel, displayName, description, scopeKinds, selfRemovable);
    }

    var server = ImmutableArray.Create(PermissionScopeKind.Server);
    var tenant = ImmutableArray.Create(PermissionScopeKind.Tenant);
    // Server scope on device permissions is only grantable to server-kind service accounts.
    // ValidatePermissionScope rejects it for others. Preset seeding uses
    // GetBroadestTenantLegalScope, which excludes Server.
    var deviceResources = ImmutableArray.Create(PermissionScopeKind.Device, PermissionScopeKind.DeviceGroup, PermissionScopeKind.CustomerTenant, PermissionScopeKind.Tenant, PermissionScopeKind.Server);
    var deviceGroup = ImmutableArray.Create(PermissionScopeKind.DeviceGroup, PermissionScopeKind.Tenant);
    var userGroup = ImmutableArray.Create(PermissionScopeKind.UserGroup, PermissionScopeKind.Tenant);

    Add(PermissionNames.ServerAuthorizationLogsRead, PermissionCategories.Servers, "Read Server Authorization Logs", "View authorization change logs across all tenants, including server-scoped entries.", server);
    Add(PermissionNames.ServerPermissionsRead, PermissionCategories.Servers, "Read Server Permission Assignments", "View server-scoped permission assignments.", server);
    Add(PermissionNames.ServerPermissionsWrite, PermissionCategories.Servers, "Manage Server Permission Assignments", "Create, update, and delete server-scoped permission assignments.", server, selfRemovable: false);
    Add(PermissionNames.ServerSettingsWrite, PermissionCategories.Servers, "Manage Server Settings", "Configure server settings: the server-wide alert and SMTP test email.", server);
    Add(PermissionNames.ServerTenantsDelete, PermissionCategories.Servers, "Delete Server Tenants", "Delete tenants from the server.", server);
    Add(PermissionNames.ServerTenantsRead, PermissionCategories.Servers, "Read Server Tenants", "List all tenants on the server.", server);
    Add(PermissionNames.ServerTenantsWrite, PermissionCategories.Servers, "Manage Server Tenants", "Create and update tenants on the server.", server);
    Add(PermissionNames.ServerTelemetryRead, PermissionCategories.Servers, "Read Server Telemetry", "View server telemetry (logs and metrics).", server);
    Add(PermissionNames.ServerServiceAccountsRead, PermissionCategories.Servers, "Read Server Service Accounts", "View server-scoped service accounts and credentials.", server);
    Add(PermissionNames.ServerServiceAccountsWrite, PermissionCategories.Servers, "Manage Server Service Accounts", "Create and delete server-scoped service accounts.", server);
    Add(PermissionNames.ServerServiceAccountsRotateCredentials, PermissionCategories.Servers, "Rotate Server Service Account Credentials", "Create and revoke credentials for server-scoped service accounts.", server);

    Add(PermissionNames.TenantRead, PermissionCategories.Tenants, "Read Tenant", "View tenant details and settings.", tenant);
    Add(PermissionNames.TenantSettingsRead, PermissionCategories.Tenants, "Read Tenant Settings", "View tenant configuration.", tenant);
    Add(PermissionNames.TenantSettingsWrite, PermissionCategories.Tenants, "Manage Tenant Settings", "Modify tenant configuration.", tenant);
    Add(PermissionNames.TenantUsersRead, PermissionCategories.Tenants, "Read Tenant Users", "View users within the tenant.", tenant);
    Add(PermissionNames.TenantUsersWrite, PermissionCategories.Tenants, "Manage Tenant Users", "Create and update users within the tenant.", tenant);
    Add(PermissionNames.TenantUsersDelete, PermissionCategories.Tenants, "Delete Tenant Users", "Remove users from the tenant.", tenant);
    Add(PermissionNames.TenantUserGroupsRead, PermissionCategories.Tenants, "Read User Groups", "View user groups within the tenant.", tenant);
    Add(PermissionNames.TenantUserGroupsWrite, PermissionCategories.Tenants, "Manage User Groups", "Create, update, and delete user groups within the tenant.", tenant);
    Add(PermissionNames.TenantDeviceGroupsRead, PermissionCategories.Tenants, "Read Device Groups", "View device groups within the tenant.", tenant);
    Add(PermissionNames.TenantDeviceGroupsWrite, PermissionCategories.Tenants, "Manage Device Groups", "Create, update, and delete device groups within the tenant.", tenant);
    Add(PermissionNames.TenantCustomersRead, PermissionCategories.Tenants, "Read Customers", "View customers within the tenant.", tenant);
    Add(PermissionNames.TenantCustomersWrite, PermissionCategories.Tenants, "Manage Customers", "Create, update, and delete customers within the tenant.", tenant);
    Add(PermissionNames.TenantTagsWrite, PermissionCategories.Tenants, "Manage Tags", "Create, update, and delete tag definitions within the tenant.", tenant);
    Add(PermissionNames.TenantPermissionsRead, PermissionCategories.Tenants, "Read Permissions", "View permission assignments within the tenant.", tenant);
    Add(PermissionNames.TenantAuthorizationLogsRead, PermissionCategories.Tenants, "Read Authorization Logs", "View the tenant's authorization change log.", tenant);
    Add(PermissionNames.TenantPermissionsWrite, PermissionCategories.Tenants, "Manage Permissions", "Create and update allow permission assignments within the tenant.", tenant, selfRemovable: false);
    Add(PermissionNames.TenantPermissionsDeny, PermissionCategories.Tenants, "Manage Deny Permissions", "Create and update deny permission assignments. Required for deny-effect assignments at any scope, including server scope.", tenant, selfRemovable: false);

    Add(PermissionNames.DeviceRead, PermissionCategories.Devices, "Read Device", "View device details and status.", deviceResources);
    Add(PermissionNames.DeviceDelete, PermissionCategories.Devices, "Delete Device", "Remove a device from the system.", deviceResources);
    Add(PermissionNames.DeviceAliasWrite, PermissionCategories.Devices, "Update Device Alias", "Change the display alias for a device.", deviceResources);
    Add(PermissionNames.DeviceTagsRead, PermissionCategories.Devices, "Read Device Tags", "View tags assigned to a device.", deviceResources);
    Add(PermissionNames.DeviceTagsWrite, PermissionCategories.Devices, "Manage Device Tags", "Add and remove tags on a device.", deviceResources);
    Add(PermissionNames.DeviceDesktopPreviewRead, PermissionCategories.Devices, "View Desktop Preview", "View the desktop preview thumbnail for a device.", deviceResources);
    Add(PermissionNames.DeviceLogsRead, PermissionCategories.Devices, "Read Device Logs", "View remote log files from a device.", deviceResources);
    Add(PermissionNames.DeviceOverviewRead, PermissionCategories.Devices, "Read Device Overview", "View the overview page for a device.", deviceResources);

    Add(PermissionNames.DeviceRemoteControlConnect, PermissionCategories.Devices, "Connect Remote Control", "Initiate a remote control session to a device.", deviceResources);
    Add(PermissionNames.DeviceRemoteControlInteract, PermissionCategories.Devices, "Interact Remote Control", "Send input during a remote control session.", deviceResources);
    Add(PermissionNames.DeviceRemoteControlBlockInput, PermissionCategories.Devices, "Block Remote Input", "Block the remote user's keyboard and mouse during a remote control session.", deviceResources);
    Add(PermissionNames.DeviceRemoteControlElevatedDesktop, PermissionCategories.Devices, "Elevated Desktop Access", "Access the elevated (system) desktop during remote control.", deviceResources);
    Add(PermissionNames.DeviceCtrlAltDelSend, PermissionCategories.Devices, "Send Ctrl+Alt+Del", "Send Ctrl+Alt+Del to a remote device.", deviceResources);
    Add(PermissionNames.DeviceClipboardRead, PermissionCategories.Devices, "Read Device Clipboard", "Read the clipboard contents from a remote device.", deviceResources);
    Add(PermissionNames.DeviceClipboardWrite, PermissionCategories.Devices, "Write Device Clipboard", "Write to the clipboard on a remote device.", deviceResources);
    Add(PermissionNames.DeviceChatSend, PermissionCategories.Devices, "Chat with Device", "Send chat messages to a remote device user.", deviceResources);
    Add(PermissionNames.DeviceVncRelayConnect, PermissionCategories.Devices, "Connect VNC Relay", "Connect to a VNC server through a remote device.", deviceResources);

    Add(PermissionNames.DeviceFileSystemRead, PermissionCategories.Devices, "Read Device File System", "Browse and read files on a remote device.", deviceResources);
    Add(PermissionNames.DeviceFileSystemWrite, PermissionCategories.Devices, "Write Device File System", "Create and modify files on a remote device.", deviceResources);
    Add(PermissionNames.DeviceFileSystemDelete, PermissionCategories.Devices, "Delete Device Files", "Delete files on a remote device.", deviceResources);
    Add(PermissionNames.DeviceFileSystemTransferUpload, PermissionCategories.Devices, "Upload Files to Device", "Upload files to a remote device.", deviceResources);
    Add(PermissionNames.DeviceFileSystemTransferDownload, PermissionCategories.Devices, "Download Files from Device", "Download files from a remote device.", deviceResources);

    Add(PermissionNames.DeviceTerminalUse, PermissionCategories.Devices, "Use Remote Terminal", "Open a terminal session and execute commands on a remote device.", deviceResources);
    Add(PermissionNames.DeviceLogonTokenCreate, PermissionCategories.Devices, "Create Logon Token", "Create a single-use logon token for a device.", deviceResources);
    Add(PermissionNames.DeviceWakeSend, PermissionCategories.Devices, "Send Wake Command", "Send a wake-on-LAN command to a device.", deviceResources);
    Add(PermissionNames.DevicePowerManage, PermissionCategories.Devices, "Manage Device Power", "Shutdown or restart a remote device.", deviceResources);
    Add(PermissionNames.DeviceAgentUpdate, PermissionCategories.Devices, "Update Device Agent", "Trigger an agent update on a remote device.", deviceResources);

    Add(PermissionNames.PersonalAccessTokenSelfRead, PermissionCategories.PersonalAccessTokens, "Read Own PATs", "View your own personal access tokens.", tenant);
    Add(PermissionNames.PersonalAccessTokenSelfWrite, PermissionCategories.PersonalAccessTokens, "Manage Own PATs", "Create and delete your own personal access tokens.", tenant);
    Add(PermissionNames.PersonalAccessTokenOthersRead, PermissionCategories.PersonalAccessTokens, "Read Others' PATs", "View personal access tokens belonging to other users in the tenant.", tenant);
    Add(PermissionNames.PersonalAccessTokenOthersWrite, PermissionCategories.PersonalAccessTokens, "Manage Others' PATs", "Create and delete personal access tokens for other users in the tenant.", tenant);
    Add(PermissionNames.ServiceAccountRead, PermissionCategories.ServiceAccounts, "Read Service Accounts", "View tenant-scoped service accounts and credentials.", tenant);
    Add(PermissionNames.ServiceAccountWrite, PermissionCategories.ServiceAccounts, "Manage Service Accounts", "Create and delete tenant-scoped service accounts.", tenant);
    Add(PermissionNames.ServiceAccountRotateCredentials, PermissionCategories.ServiceAccounts, "Rotate Service Account Credentials", "Create and revoke credentials for tenant-scoped service accounts.", tenant);

    Add(PermissionNames.InstallerKeyRead, PermissionCategories.InstallerKeys, "Read Installer Keys", "View agent installer keys.", tenant);
    Add(PermissionNames.InstallerKeyWrite, PermissionCategories.InstallerKeys, "Manage Installer Keys", "Create and delete agent installer keys.", tenant);
    Add(PermissionNames.InstallerKeyManageAll, PermissionCategories.InstallerKeys, "Manage All Installer Keys", "View and manage installer keys created by any user in the tenant.", tenant);
    Add(PermissionNames.AgentInstall, PermissionCategories.Agents, "Install Agent", "Generate agent installation commands and scripts.", tenant);

    Add(PermissionNames.DeviceGroupAssignDevices, PermissionCategories.DeviceGroups, "Assign Devices to Group", "Add and remove devices from a device group.", deviceGroup);
    Add(PermissionNames.UserGroupAssignUsers, PermissionCategories.UserGroups, "Assign Users to Group", "Add and remove users from a user group.", userGroup);

    return catalog.ToFrozenDictionary();
  }
}
