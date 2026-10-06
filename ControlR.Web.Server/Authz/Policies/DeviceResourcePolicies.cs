namespace ControlR.Web.Server.Authz.Policies;

/// <summary>
/// Resource-based authorization policies scoped to a specific device. Each policy requires a
/// single device permission evaluated against the target device (passed as the authorization
/// resource via <c>AuthorizeAsync(user, device, DeviceResourcePolicies.X)</c>).
/// </summary>
public static class DeviceResourcePolicies
{
  public const string AgentUpdate = "DeviceAgentUpdate";
  public const string AliasWrite = "DeviceAliasWrite";
  public const string ChatSend = "DeviceChatSend";
  public const string ClipboardRead = "DeviceClipboardRead";
  public const string ClipboardWrite = "DeviceClipboardWrite";
  public const string CtrlAltDelSend = "DeviceCtrlAltDelSend";
  public const string Delete = "DeviceDelete";
  public const string DesktopPreviewRead = "DeviceDesktopPreviewRead";
  public const string FileSystemDelete = "DeviceFileSystemDelete";
  public const string FileSystemRead = "DeviceFileSystemRead";
  public const string FileSystemTransferDownload = "DeviceFileSystemTransferDownload";
  public const string FileSystemTransferUpload = "DeviceFileSystemTransferUpload";
  public const string FileSystemWrite = "DeviceFileSystemWrite";
  public const string LogonTokenCreate = "DeviceLogonTokenCreate";
  public const string LogsRead = "DeviceLogsRead";
  public const string OverviewRead = "DeviceOverviewRead";
  public const string PowerManage = "DevicePowerManage";
  public const string Read = "DeviceRead";
  public const string RemoteControlBlockInput = "DeviceRemoteControlBlockInput";
  public const string RemoteControlConnect = "DeviceRemoteControlConnect";
  public const string RemoteControlInteract = "DeviceRemoteControlInteract";
  public const string TagsWrite = "DeviceTagsWrite";
  public const string TerminalUse = "DeviceTerminalUse";
  public const string VncRelayConnect = "DeviceVncRelayConnect";
  public const string WakeSend = "DeviceWakeSend";

  public static IReadOnlyDictionary<string, string> PolicyToPermission { get; } =
    new Dictionary<string, string>
    {
      [Read] = PermissionNames.DeviceRead,
      [AgentUpdate] = PermissionNames.DeviceAgentUpdate,
      [RemoteControlConnect] = PermissionNames.DeviceRemoteControlConnect,
      [RemoteControlInteract] = PermissionNames.DeviceRemoteControlInteract,
      [RemoteControlBlockInput] = PermissionNames.DeviceRemoteControlBlockInput,
      [ClipboardRead] = PermissionNames.DeviceClipboardRead,
      [ClipboardWrite] = PermissionNames.DeviceClipboardWrite,
      [Delete] = PermissionNames.DeviceDelete,
      [AliasWrite] = PermissionNames.DeviceAliasWrite,
      [ChatSend] = PermissionNames.DeviceChatSend,
      [CtrlAltDelSend] = PermissionNames.DeviceCtrlAltDelSend,
      [DesktopPreviewRead] = PermissionNames.DeviceDesktopPreviewRead,
      [TerminalUse] = PermissionNames.DeviceTerminalUse,
      [LogonTokenCreate] = PermissionNames.DeviceLogonTokenCreate,
      [LogsRead] = PermissionNames.DeviceLogsRead,
      [OverviewRead] = PermissionNames.DeviceOverviewRead,
      [PowerManage] = PermissionNames.DevicePowerManage,
      [TagsWrite] = PermissionNames.DeviceTagsWrite,
      [FileSystemRead] = PermissionNames.DeviceFileSystemRead,
      [FileSystemWrite] = PermissionNames.DeviceFileSystemWrite,
      [FileSystemDelete] = PermissionNames.DeviceFileSystemDelete,
      [FileSystemTransferDownload] = PermissionNames.DeviceFileSystemTransferDownload,
      [FileSystemTransferUpload] = PermissionNames.DeviceFileSystemTransferUpload,
      [VncRelayConnect] = PermissionNames.DeviceVncRelayConnect,
      [WakeSend] = PermissionNames.DeviceWakeSend,
    };
}
