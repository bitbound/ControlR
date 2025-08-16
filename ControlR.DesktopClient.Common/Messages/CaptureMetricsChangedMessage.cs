using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.DesktopClient.Common.Messages;
public record CaptureMetricsChangedMessage(CaptureMetricsDto MetricsDto);