namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record InitiateChunkedUploadRequestDto(
  Guid DeviceId,
  string TargetDirectory,
  string FileName,
  long TotalSize,
  bool Overwrite);

public record InitiateChunkedUploadResponseDto(
  Guid UploadId,
  int ChunkSize,
  long MaxFileSize);

public record UploadChunkRequestDto(
  Guid UploadId,
  int ChunkIndex,
  int TotalChunks);

public record UploadChunkResponseDto(
  bool Success,
  string? Message);

public record CompleteChunkedUploadRequestDto(
  Guid UploadId);

public record CompleteChunkedUploadResponseDto(
  bool Success,
  string? Message);
