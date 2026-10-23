using ControlR.Libraries.Api.Contracts.Dtos;
using System.Text.Json;

namespace ControlR.ApiClient;

internal static class HttpExtensions
{
  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static async Task EnsureSuccessStatusCodeWithDetails(this HttpResponseMessage response)
  {
    if (!response.IsSuccessStatusCode)
    {
      var exception = await TryGetEnrichedException(response);
      if (exception is not null)
      {
        throw exception;
      }

      // Fall back to the default behavior if we couldn't enrich the exception.
      response.EnsureSuccessStatusCode();
    }
  }

  private static string EnrichErrorMessage(string rawContent, ProblemDetailsDto? problemDetails)
  {
    if (problemDetails is { } pd)
    {
      var bestMessage = GetBestMessage(pd);
      if (pd.Status.HasValue)
      {
        return $"[Status: {pd.Status}] {bestMessage}";
      }

      return bestMessage;
    }

    // Not a ProblemDetails body. The deprecated /api/* endpoints this client still calls answer some
    // failures with a bare JSON string. The /api/v1 failures answer with a ProblemDetails body.
    var trimmed = rawContent.Trim();
    if (trimmed.Length > 2 && trimmed[0] == '"' && trimmed[^1] == '"')
    {
      try
      {
        return JsonSerializer.Deserialize<string>(trimmed) ?? trimmed;
      }
      catch
      {
        return trimmed[1..^1];
      }
    }

    return string.IsNullOrWhiteSpace(trimmed) ? "An unexpected error occurred." : trimmed;
  }

  private static string GetBestMessage(ProblemDetailsDto problemDetails)
  {
    return problemDetails.Detail ?? problemDetails.Title ?? "An error occurred.";
  }


  private static async Task<HttpRequestException?> TryGetEnrichedException(HttpResponseMessage response)
  {
    try
    {
      var rawContent = await response.Content.ReadAsStringAsync();

      ProblemDetailsDto? problemDetails = null;
      try
      {
        problemDetails = JsonSerializer.Deserialize<ProblemDetailsDto>(rawContent, _jsonOptions);
      }
      catch
      {
        // The body is not a JSON object: an HTML error page, a plain-text status page, or one of
        // the bare JSON strings the deprecated /api/* endpoints return for some failures.
      }

      var enrichedMessage = EnrichErrorMessage(rawContent, problemDetails);
      return new HttpRequestException(enrichedMessage, null, response.StatusCode);
    }
    catch
    {
      return null;
    }
  }
}
