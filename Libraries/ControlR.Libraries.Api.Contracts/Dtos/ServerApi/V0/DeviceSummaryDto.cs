namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

[MessagePackObject(keyAsPropertyName: true)]
public record DeviceSummaryDto(
    Guid Id,
    DateTimeOffset LastSeen,
    string AgentVersion);
