namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public enum AssistSessionEndedReason
{
  EndedByPeer,
  PeerDisconnected,
  AccessCodeExpired,
  SessionExpired,
}
