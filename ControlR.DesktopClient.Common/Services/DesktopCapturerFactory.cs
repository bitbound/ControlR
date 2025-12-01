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
            CaptureEncoderType.Vpx => CreateStreamBasedCapturer(),
            _ => throw new NotSupportedException($"Encoder type {_options.Value.EncoderType} is not supported.")
        };
    }

    private IDesktopCapturer CreateStreamBasedCapturer()
    {
        var encoder = ActivatorUtilities.CreateInstance<Vp9Encoder>(_serviceProvider);
        return ActivatorUtilities.CreateInstance<StreamBasedCapturer>(_serviceProvider, encoder);
    }
}
