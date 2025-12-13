using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Web.Client.Services;

public interface IPublicRegistrationSettingsProvider
{
  Task<bool> GetIsPublicRegistrationEnabled();
}
