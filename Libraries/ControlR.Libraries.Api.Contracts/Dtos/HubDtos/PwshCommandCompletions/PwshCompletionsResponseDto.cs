namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;

public record PwshCompletionsResponseDto(
    int ReplacementIndex,
    int ReplacementLength,
    PwshCompletionMatch[] CompletionMatches,
    bool HasMorePages = false,
    int TotalCount = 0,
    int CurrentPage = 0
)
{
    public const int MaxRetrievableItems = 500;
};