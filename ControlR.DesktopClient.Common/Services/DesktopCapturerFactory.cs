using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopCapturerFactory
{
  /// <summary>
  ///   Creates a new instance of IDesktopCapturer, discarding any existing instance.
  /// </summary>
  /// <returns>
  ///   The new IDesktopCapturer instance.
  /// </returns>
  /// 
  IDesktopCapturer CreateNew();
  /// <summary>
  ///   Gets the existing IDesktopCapturer instance, or creates one if it doesn't exist.
  /// </summary>
  /// <returns>
  ///   The existing or newly created IDesktopCapturer instance.
  /// </returns>
  IDesktopCapturer GetOrCreate();
}

public class DesktopCapturerFactory(IServiceProvider serviceProvider, IOptions<RemoteControlSessionOptions> options) : IDesktopCapturerFactory
{
  private readonly Lock _createLock = new();
  private readonly IOptions<RemoteControlSessionOptions> _options = options;
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  private IDesktopCapturer? _capturer;

  public IDesktopCapturer CreateNew()
  {
    using var lockScope = _createLock.EnterScope();
    return CreateCapturer();
  }

  public IDesktopCapturer GetOrCreate()
  {
    using var lockScope = _createLock.EnterScope();

    if (_capturer is not null)
    {
      return _capturer;
    }

    return CreateCapturer();
  }

  private IDesktopCapturer CreateCapturer()
  {
    _capturer = _options.Value.EncoderType switch
    {
      CaptureEncoderType.Jpeg => ActivatorUtilities.CreateInstance<FrameBasedCapturer>(_serviceProvider),
      CaptureEncoderType.Vpx => CreateStreamBasedCapturer(),
      _ => throw new NotSupportedException($"Encoder type {_options.Value.EncoderType} is not supported.")
    };
    return _capturer;
  }

  private StreamBasedCapturer CreateStreamBasedCapturer()
  {
    var encoder = ActivatorUtilities.CreateInstance<Vp9Encoder>(_serviceProvider);
    return ActivatorUtilities.CreateInstance<StreamBasedCapturer>(_serviceProvider, encoder);
  }
}