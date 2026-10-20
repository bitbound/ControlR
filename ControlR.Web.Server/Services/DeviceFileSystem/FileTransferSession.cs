namespace ControlR.Web.Server.Services.DeviceFileSystem;

/// <summary>
/// A transfer the agent is filling. The caller drains <see cref="ReadChunks" /> into its response and
/// disposes the session when the transfer ends, which releases the stream session the agent writes to.
/// </summary>
/// <param name="signaler">The channel the agent is writing this transfer's chunks into.</param>
/// <param name="fileName">The name for the response's <c>Content-Disposition</c> header.</param>
/// <param name="fileSize">
/// The byte count the agent reported, or null when the transfer has no length to state, as with a log
/// file's text.
/// </param>
/// <param name="cancellationToken">Stops the read when the caller abandons the response.</param>
public sealed class FileTransferSession(
  HubStreamSignaler<byte[]> signaler,
  string fileName,
  long? fileSize,
  CancellationToken cancellationToken) : IDisposable
{
  private readonly CancellationToken _cancellationToken = cancellationToken;
  private readonly HubStreamSignaler<byte[]> _signaler = signaler;

  public string FileName { get; } = fileName;
  public long? FileSize { get; } = fileSize;

  public void Dispose()
  {
    _signaler.Dispose();
  }

  public IAsyncEnumerable<byte[]> ReadChunks()
  {
    return _signaler.Reader.ReadAllAsync(_cancellationToken);
  }
}
