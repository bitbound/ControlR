namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;

public record PwshCompletionMatch(
    string CompletionText,
    string ListItemText,
    PwshCompletionMatchType MatchType,
    string ToolTip
);