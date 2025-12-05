namespace ControlR.Web.Server.Extensions;

public static class HubStreamSignalerExtensions
{
  public static async Task WriteFromStream(this HubStreamSignaler<byte[]> signaler, Stream sourceStream, CancellationToken cancellationToken)
  {
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, signaler.WriteCompleted);
    var buffer = new byte[81920];
    int bytesRead;

    try
    {
      while ((bytesRead = await sourceStream.ReadAsync(buffer, linkedCts.Token)) > 0)
      {
        await signaler.Writer.WriteAsync(buffer[..bytesRead], linkedCts.Token);
      }
      signaler.SetWriteCompleted();
    }
    catch (Exception ex)
    {
      signaler.SetWriteCompleted(ex);
      throw;
    }
  }
}