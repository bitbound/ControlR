namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SendChatMessageRequestDto(
  Guid DeviceId,
  ChatMessageHubDto Message);
