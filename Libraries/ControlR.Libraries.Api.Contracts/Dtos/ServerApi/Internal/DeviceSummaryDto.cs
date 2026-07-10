namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record DeviceSummaryDto(
    Guid Id,
    DateTimeOffset LastSeen,
    string AgentVersion);
