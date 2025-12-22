using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.Messages;
public record CaptureMetricsChangedMessage(CaptureMetricsDto MetricsDto);