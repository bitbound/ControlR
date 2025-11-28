using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Common.Services;

public interface ISessionConsentService
{
  Task<bool> RequestConsentAsync(string requesterName, CancellationToken cancellationToken);
}


public class SessionConsentService(
  IUserInteractionService userInteractionService,
  ILogger<SessionConsentService> logger) : ISessionConsentService
{
  private readonly ILogger<SessionConsentService> _logger = logger;
  private readonly IUserInteractionService _userInteractionService = userInteractionService;

  public async Task<bool> RequestConsentAsync(string requesterName, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Requesting user consent for session from {RequesterName}", requesterName);
    
    try
    {
      var granted = await _userInteractionService.ShowConsentDialogAsync(requesterName, cancellationToken);
      
      if (granted)
      {
        _logger.LogInformation("User granted consent for session from {RequesterName}", requesterName);
      }
      else
      {
        _logger.LogWarning("User denied consent for session from {RequesterName}", requesterName);
      }

      return granted;
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("Consent request timed out or was canceled for {RequesterName}", requesterName);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error requesting consent for session from {RequesterName}", requesterName);
      // Fail safe: deny if error
      return false;
    }
  }
}
