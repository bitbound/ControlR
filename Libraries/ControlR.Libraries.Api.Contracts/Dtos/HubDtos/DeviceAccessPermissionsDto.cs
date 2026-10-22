namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public sealed record DeviceAccessPermissionsDto(
  bool CanViewOverview,
  bool CanUseRemoteControl,
  bool CanUseTerminal,
  bool CanUseChat,
  bool CanReadFileSystem,
  bool CanReadLogs,
  bool CanUseVncRelay,
  bool CanInteractWithRemoteControl,
  bool CanBlockRemoteInput,
  bool CanReadClipboard,
  bool CanWriteClipboard,
  bool CanSendCtrlAltDel,
  bool CanTogglePrivacyScreen);
