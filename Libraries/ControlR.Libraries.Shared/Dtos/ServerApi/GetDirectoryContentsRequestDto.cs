using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record GetDirectoryContentsRequestDto(
  Guid DeviceId,
  string DirectoryPath);
