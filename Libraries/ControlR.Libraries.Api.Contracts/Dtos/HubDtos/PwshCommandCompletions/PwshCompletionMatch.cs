namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;

[MessagePackObject(keyAsPropertyName: true)]
public record PwshCompletionMatch(
    string CompletionText,
    string ListItemText,
    PwshCompletionMatchType MatchType,
    string ToolTip
);