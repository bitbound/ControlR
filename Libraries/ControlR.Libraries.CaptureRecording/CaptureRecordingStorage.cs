using System.Buffers.Binary;

namespace ControlR.Libraries.CaptureRecording;

internal enum CaptureRecordKind : byte
{
  KeyFrame = 1,
  FrameBatch = 2,
  Event = 3,
}

[Flags]
internal enum CaptureRecordFlags : byte
{
  None = 0,
}

internal static class CaptureRecordingStorage
{
  public const short CurrentVersion = 1;
  public const short FileHeaderSize = 64;
  public const int IndexEntrySize = 32;
  public const int RecordHeaderSize = 32;

  public static CaptureFileHeader ReadFileHeader(Stream stream)
  {
    ArgumentNullException.ThrowIfNull(stream);

    stream.Position = 0;

    Span<byte> buffer = stackalloc byte[FileHeaderSize];
    stream.ReadExactly(buffer);

    var version = BinaryPrimitives.ReadInt16LittleEndian(buffer[..2]);
    var headerSize = BinaryPrimitives.ReadInt16LittleEndian(buffer[2..4]);
    var fileLength = BinaryPrimitives.ReadInt64LittleEndian(buffer[4..12]);
    var recordingStartUtcTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[12..20]);
    var indexOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer[20..28]);
    var indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(buffer[28..32]);
    var flags = BinaryPrimitives.ReadInt32LittleEndian(buffer[32..36]);

    return new CaptureFileHeader(
      version,
      headerSize,
      fileLength,
      recordingStartUtcTicks,
      indexOffset,
      indexEntryCount,
      flags);
  }

  public static CaptureIndexEntry ReadIndexEntry(Stream stream)
  {
    Span<byte> buffer = stackalloc byte[IndexEntrySize];
    stream.ReadExactly(buffer);

    var offset = BinaryPrimitives.ReadInt64LittleEndian(buffer[..8]);
    var timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[8..16]);
    var sequence = BinaryPrimitives.ReadInt32LittleEndian(buffer[16..20]);
    var nearestKeyFrameOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer[20..28]);
    var kind = (CaptureRecordKind)buffer[28];

    return new CaptureIndexEntry(
      offset,
      timestampTicks,
      sequence,
      nearestKeyFrameOffset,
      kind);
  }

  public static CaptureRecord ReadRecord(Stream stream, long offset)
  {
    stream.Position = offset;

    Span<byte> headerBuffer = stackalloc byte[RecordHeaderSize];
    stream.ReadExactly(headerBuffer);

    var header = new CaptureRecordHeader(
      (CaptureRecordKind)headerBuffer[0],
      (CaptureRecordFlags)headerBuffer[1],
      BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[4..8]),
      BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[8..16]),
      BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[16..20]),
      BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[20..24]),
      BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[24..28]));

    var payload = new byte[header.PayloadLength];
    stream.ReadExactly(payload);

    return new CaptureRecord(offset, header, payload);
  }

  public static void ValidateFileHeader(CaptureFileHeader header)
  {
    if (header.Version != CurrentVersion)
    {
      throw new InvalidDataException(
        $"Unsupported capture recording version {header.Version}. Expected {CurrentVersion}.");
    }

    if (header.HeaderSize != FileHeaderSize)
    {
      throw new InvalidDataException(
        $"Unsupported capture recording header size {header.HeaderSize}. Expected {FileHeaderSize}.");
    }
  }

  public static void WriteFileHeader(Stream stream, CaptureFileHeader header)
  {
    Span<byte> buffer = stackalloc byte[FileHeaderSize];

    BinaryPrimitives.WriteInt16LittleEndian(buffer[..2], header.Version);
    BinaryPrimitives.WriteInt16LittleEndian(buffer[2..4], header.HeaderSize);
    BinaryPrimitives.WriteInt64LittleEndian(buffer[4..12], header.FileLength);
    BinaryPrimitives.WriteInt64LittleEndian(buffer[12..20], header.RecordingStartUtcTicks);
    BinaryPrimitives.WriteInt64LittleEndian(buffer[20..28], header.IndexOffset);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[28..32], header.IndexEntryCount);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[32..36], header.Flags);

    stream.Position = 0;
    stream.Write(buffer);
  }

  public static void WriteIndexEntry(Stream stream, CaptureIndexEntry entry)
  {
    Span<byte> buffer = stackalloc byte[IndexEntrySize];

    BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], entry.Offset);
    BinaryPrimitives.WriteInt64LittleEndian(buffer[8..16], entry.TimestampTicks);
    BinaryPrimitives.WriteInt32LittleEndian(buffer[16..20], entry.Sequence);
    BinaryPrimitives.WriteInt64LittleEndian(buffer[20..28], entry.NearestKeyFrameOffset);
    buffer[28] = (byte)entry.Kind;

    stream.Write(buffer);
  }

  public static void WriteRecord(Stream stream, CaptureRecordHeader header, byte[] payload)
  {
    Span<byte> headerBuffer = stackalloc byte[RecordHeaderSize];

    headerBuffer[0] = (byte)header.Kind;
    headerBuffer[1] = (byte)header.Flags;
    BinaryPrimitives.WriteInt32LittleEndian(headerBuffer[4..8], header.Sequence);
    BinaryPrimitives.WriteInt64LittleEndian(headerBuffer[8..16], header.TimestampTicks);
    BinaryPrimitives.WriteInt32LittleEndian(headerBuffer[16..20], header.PayloadLength);
    BinaryPrimitives.WriteInt32LittleEndian(headerBuffer[20..24], header.CanvasWidth);
    BinaryPrimitives.WriteInt32LittleEndian(headerBuffer[24..28], header.CanvasHeight);

    stream.Write(headerBuffer);
    stream.Write(payload);
  }
}

internal readonly record struct CaptureFileHeader(
  short Version,
  short HeaderSize,
  long FileLength,
  long RecordingStartUtcTicks,
  long IndexOffset,
  int IndexEntryCount,
  int Flags);

internal readonly record struct CaptureIndexEntry(
  long Offset,
  long TimestampTicks,
  int Sequence,
  long NearestKeyFrameOffset,
  CaptureRecordKind Kind);

internal readonly record struct CaptureRecord(
  long Offset,
  CaptureRecordHeader Header,
  byte[] Payload);

internal readonly record struct CaptureRecordHeader(
  CaptureRecordKind Kind,
  CaptureRecordFlags Flags,
  int Sequence,
  long TimestampTicks,
  int PayloadLength,
  int CanvasWidth,
  int CanvasHeight);
