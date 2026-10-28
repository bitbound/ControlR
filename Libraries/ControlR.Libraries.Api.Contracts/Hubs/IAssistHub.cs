using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Hubs;

/// <summary>
/// Control plane for ad-hoc remote control sessions against unmanaged devices.
/// The assist app side is anonymous and authenticated only by its one-time code.
/// The technician side additionally requires a signed-in user.
/// </summary>
public interface IAssistHub
{
  Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession();
  Task<HubResult> EndAssistSession(EndAssistSessionRequestDto request);
  Task<HubResult<JoinAssistSessionResponseDto>> JoinAssistSession(JoinAssistSessionRequestDto request);
  Task<HubResult> RespondToAssistConsent(RespondToAssistConsentRequestDto request);
}
