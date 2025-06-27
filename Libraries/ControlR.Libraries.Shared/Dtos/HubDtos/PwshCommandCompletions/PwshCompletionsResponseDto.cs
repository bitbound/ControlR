namespace ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;


public record PwshCompletionsResponseDto(
    int CurrentMatchIndex,
    int ReplacementIndex,
    int ReplacementLength,
    PwshCompletionMatch[] CompletionMatches
);