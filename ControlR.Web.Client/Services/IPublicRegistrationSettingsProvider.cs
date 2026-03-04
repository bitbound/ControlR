using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.Web.Client.Services;

public interface IPublicRegistrationSettingsProvider
{
  Task<bool> GetIsPublicRegistrationEnabled();
}
