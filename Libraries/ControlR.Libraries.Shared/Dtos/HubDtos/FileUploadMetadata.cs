namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record FileUploadMetadata(
  Guid DeviceId,
  string TargetDirectory,
  string FileName,
  long FileSize,
  string ContentType,
  bool Overwrite);