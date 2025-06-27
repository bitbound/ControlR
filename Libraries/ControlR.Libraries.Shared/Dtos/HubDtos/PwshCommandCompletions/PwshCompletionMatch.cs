namespace ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;

public record PwshCompletionMatch(
    string CompletionText,
    string ListItemText,
    PwshCompletionMatchType MatchType,
    string ToolTip
);