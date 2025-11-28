using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services.Encoders;
using ControlR.Libraries.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

public interface IDesktopCapturerFactory
{
    IDesktopCapturer Create();
}

public class DesktopCapturerFactory(IServiceProvider serviceProvider, IOptions<StreamingSessionOptions> options) : IDesktopCapturerFactory
{
    private readonly IOptions<StreamingSessionOptions> _options = options;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IDesktopCapturer Create()
    {
        return _options.Value.EncoderType switch
        {
            CaptureEncoderType.Jpeg => ActivatorUtilities.CreateInstance<FrameBasedCapturer>(_serviceProvider),
            CaptureEncoderType.H264 => CreateStreamBasedCapturer(),
            _ => throw new NotSupportedException($"Encoder type {_options.Value.EncoderType} is not supported.")
        };
    }

    private IDesktopCapturer CreateStreamBasedCapturer()
    {
        // We need to provide IStreamEncoder.
        // We can create H264Encoder here or resolve it if registered.
        // Since H264Encoder is stateful and disposable, creating it here is fine.
        var encoder = ActivatorUtilities.CreateInstance<H264Encoder>(_serviceProvider);
        return ActivatorUtilities.CreateInstance<StreamBasedCapturer>(_serviceProvider, encoder);
    }
}
