namespace ControlR.ApiClient;

public sealed class ResponseStream(HttpResponseMessage response, Stream stream) : IAsyncDisposable
{
  public HttpResponseMessage Response { get; } = response;
  public Stream Stream { get; } = stream;

  public async ValueTask DisposeAsync()
  {
    await Stream.DisposeAsync();
    Response.Dispose();
  }
}