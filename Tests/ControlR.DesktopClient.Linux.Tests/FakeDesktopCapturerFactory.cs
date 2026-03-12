using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using System.Runtime.CompilerServices;

namespace ControlR.DesktopClient.Linux.Tests;

internal class FakeDesktopCapturerFactory : IDesktopCapturerFactory
{
  public IDesktopCapturer CreateNew() => new FakeDesktopCapturer();
  public IDesktopCapturer GetOrCreate() => new FakeDesktopCapturer();

  private class FakeDesktopCapturer : IDesktopCapturer
  {
    public Task ChangeDisplays(string displayId) => Task.CompletedTask;
    public ValueTask DisposeAsync() => default;
    public string GetCaptureMode() => string.Empty;
    public async IAsyncEnumerable<DtoWrapper> GetCaptureStream([EnumeratorCancellation] System.Threading.CancellationToken cancellationToken) { yield break; }
    public double GetCurrentFps(System.TimeSpan window) => 0;
    public Task RequestKeyFrame() => Task.CompletedTask;
    public Task StartCapturingChanges(System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<Result<ControlR.DesktopClient.Common.Models.DisplayInfo>> TryGetSelectedDisplay() => Task.FromResult(Result.Fail<DisplayInfo>("No display"));
  }
}
