namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeploymentOptions;

/// <summary>
/// Requests whether the current principal may assign tags to a prospective deployment target.
/// The target is either a predetermined existing device (by id) or a new device, optionally
/// bound to the selected customer. The server evaluates the same device-scoped
/// <c>DeviceTagsWrite</c> decision used at agent registration so the UI only offers tag
/// selection when the eventual install could succeed.
/// </summary>
public sealed record DeploymentTagCapabilityRequestDto(
  Guid? DeviceId,
  Guid? CustomerId);