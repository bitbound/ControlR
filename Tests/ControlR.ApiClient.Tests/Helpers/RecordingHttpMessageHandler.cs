using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ControlR.ApiClient.Tests.Helpers;

/// <summary>
/// A scriptable <see cref="HttpMessageHandler"/> that records every request and returns a canned
/// response. Used to exercise the API client graph without a real server.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

  private int _disposeCount;

  public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
  {
    _responder = responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK));
  }

  public int DisposeCount => _disposeCount;
  public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

  public static HttpResponseMessage Json(string payload) =>
    new(HttpStatusCode.OK)
    {
      Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      Interlocked.Increment(ref _disposeCount);
    }

    base.Dispose(disposing);
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    Requests.Enqueue(request);
    return Task.FromResult(_responder(request));
  }
}
