using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos;

/// <summary>
/// Represents an RFC 9457 Problem Details response.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public class ProblemDetailsDto
{
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }
    [JsonPropertyName("status")]
    public int? Status { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}