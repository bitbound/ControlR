using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.Messages;
public record CaptureMetricsChangedMessage(CaptureMetricsDto MetricsDto);