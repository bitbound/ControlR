using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ControlR.Libraries.Viewer.Avalonia.ViewModels.Fakes;

internal class FakeScreenRendererViewModel : IScreenRendererViewModel
{
  public FakeScreenRendererViewModel()
  {
    var channelOptions = new BoundedChannelOptions(2)
    {
      FullMode = BoundedChannelFullMode.Wait,
      SingleReader = true,
      SingleWriter = true,
    };

    FrameChannel = Channel.CreateBounded<CaptureFrame>(channelOptions);
  }
  public Channel<CaptureFrame> FrameChannel { get; }

  public ILogger Logger => throw new NotImplementedException();

  public void Dispose()
  {
    // No-op.
  }

  public IAsyncEnumerable<CaptureFrame> GetCaptureFrames(CancellationToken cancellationToken)
  {
    throw new NotImplementedException();
  }
}
