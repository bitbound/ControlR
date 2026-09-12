namespace ControlR.Libraries.Api.Contracts.Authz;

/// <summary>
/// Named permission presets: curated permission sets used to seed principals (the first/bootstrap
/// user, test users, and the role-to-permission backfill). These are the assignable templates that
/// replaced the legacy role bundles; roles no longer participate in authorization, but the curated
/// sets remain the canonical groupings of related permissions.
/// </summary>
public static class PermissionPresets
{
  public const string AgentInstaller = "Agent Installer";
  public const string DeviceSuperUser = "Device Superuser";
  public const string InstallerKeyManager = "Installer Key Manager";
  public const string SelfService = "Self Service";
  public const string ServerAdministrator = "Server Administrator";
  public const string ServiceAccountManager = "Service Account Manager";
  public const string TenantAdministrator = "Tenant Administrator";

  public static IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; } = BuildPresets();

  public static IReadOnlyList<string> GetPermissions(string presetName) =>
    All.TryGetValue(presetName, out var permissions) ? permissions : [];

  private static Dictionary<string, IReadOnlyList<string>> BuildPresets()
  {
    return new Dictionary<string, IReadOnlyList<string>>
    {
      [ServerAdministrator] =
      [
        PermissionNames.ServerAuthorizationLogsRead,
        PermissionNames.ServerTenantsDelete,
        PermissionNames.ServerTenantsRead,
        PermissionNames.ServerTenantsWrite,
        PermissionNames.ServerSettingsWrite,
        PermissionNames.ServerTelemetryRead,
        PermissionNames.ServerServiceAccountsRead,
        PermissionNames.ServerServiceAccountsWrite,
        PermissionNames.ServerServiceAccountsRotateCredentials,
        PermissionNames.ServerPermissionsRead,
        PermissionNames.ServerPermissionsWrite,
        PermissionNames.TenantPermissionsRead,
        PermissionNames.TenantAuthorizationLogsRead,
      ],

      [TenantAdministrator] =
      [
        PermissionNames.TenantRead,
        PermissionNames.TenantSettingsRead,
        PermissionNames.TenantSettingsWrite,
        PermissionNames.TenantUsersRead,
        PermissionNames.TenantUsersWrite,
        PermissionNames.TenantUsersDelete,
        PermissionNames.TenantUserGroupsRead,
        PermissionNames.TenantUserGroupsWrite,
        PermissionNames.UserGroupAssignUsers,
        PermissionNames.TenantDeviceGroupsRead,
        PermissionNames.TenantDeviceGroupsWrite,
        PermissionNames.DeviceGroupAssignDevices,
        PermissionNames.TenantCustomersRead,
        PermissionNames.TenantCustomersWrite,
        PermissionNames.TenantTagsWrite,
        PermissionNames.TenantPermissionsRead,
        PermissionNames.TenantAuthorizationLogsRead,
        PermissionNames.TenantPermissionsWrite,
        PermissionNames.TenantPermissionsDeny,
        PermissionNames.PersonalAccessTokenSelfRead,
        PermissionNames.PersonalAccessTokenSelfWrite,
        PermissionNames.PersonalAccessTokenOthersRead,
        PermissionNames.PersonalAccessTokenOthersWrite,
        PermissionNames.ServiceAccountRead,
        PermissionNames.ServiceAccountWrite,
        PermissionNames.ServiceAccountRotateCredentials,
        PermissionNames.InstallerKeyRead,
        PermissionNames.InstallerKeyWrite,
        PermissionNames.InstallerKeyManageAll,
        PermissionNames.AgentInstall,
      ],

      [DeviceSuperUser] =
      [
        PermissionNames.DeviceRead,
        PermissionNames.DeviceDelete,
        PermissionNames.DeviceAliasWrite,
        PermissionNames.DeviceTagsRead,
        PermissionNames.DeviceTagsWrite,
        PermissionNames.DeviceDesktopPreviewRead,
        PermissionNames.DeviceOverviewRead,
        PermissionNames.DeviceLogsRead,
        PermissionNames.DeviceRemoteControlConnect,
        PermissionNames.DeviceRemoteControlInteract,
        PermissionNames.DeviceRemoteControlBlockInput,
        PermissionNames.DeviceRemoteControlElevatedDesktop,
        PermissionNames.DeviceCtrlAltDelSend,
        PermissionNames.DeviceClipboardRead,
        PermissionNames.DeviceClipboardWrite,
        PermissionNames.DeviceChatSend,
        PermissionNames.DeviceFileSystemRead,
        PermissionNames.DeviceFileSystemWrite,
        PermissionNames.DeviceFileSystemDelete,
        PermissionNames.DeviceFileSystemTransferUpload,
        PermissionNames.DeviceFileSystemTransferDownload,
        PermissionNames.DeviceTerminalUse,
        PermissionNames.DeviceVncRelayConnect,
        PermissionNames.DeviceLogonTokenCreate,
        PermissionNames.DeviceWakeSend,
        PermissionNames.DevicePowerManage,
        PermissionNames.DeviceAgentUpdate,
      ],

      [AgentInstaller] =
      [
        PermissionNames.AgentInstall,
        PermissionNames.InstallerKeyRead,
        PermissionNames.InstallerKeyWrite,
      ],

      [InstallerKeyManager] =
      [
        PermissionNames.InstallerKeyRead,
        PermissionNames.InstallerKeyWrite,
        PermissionNames.AgentInstall,
      ],

      [ServiceAccountManager] =
      [
        PermissionNames.ServiceAccountRead,
        PermissionNames.ServiceAccountWrite,
        PermissionNames.ServiceAccountRotateCredentials,
      ],

      // Baseline every interactive user gets: managing their own personal access tokens.
      // Mirrors the pre-permissions behavior where the self-PAT endpoints were available to
      // any authenticated user. External (guest) users are intentionally excluded.
      [SelfService] =
      [
        PermissionNames.PersonalAccessTokenSelfRead,
        PermissionNames.PersonalAccessTokenSelfWrite,
      ],
    };
  }
}
