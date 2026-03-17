using ControlR.Libraries.Api.Contracts.Dtos;
using System.Reflection.Metadata;
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
    if (problemDetails == null)
    {
      return rawContent;
    }

    // If we have ProblemDetails, prefer its structured message.
    var bestMessage = problemDetails.GetBestMessage();

    // Optionally include the status code for context.
    if (problemDetails.Status.HasValue)
    {
      return $"[Status: {problemDetails.Status}] {bestMessage}";
    }

    return bestMessage;
  }

  private static async Task<HttpRequestException?> TryGetEnrichedException(HttpResponseMessage response)
  {
    try
    {
      var rawContent = await response.Content.ReadAsStringAsync();
      var problemDetails = JsonSerializer.Deserialize<ProblemDetailsDto>(rawContent, _jsonOptions);
      var enrichedMessage = EnrichErrorMessage(rawContent, problemDetails);

      return new HttpRequestException(enrichedMessage, null, response.StatusCode);
    }
    catch
    {
      return null;
    }
  }
}
