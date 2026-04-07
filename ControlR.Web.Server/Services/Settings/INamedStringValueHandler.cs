using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Settings;

public interface INamedStringValueHandler
{
  string Name { get; }

  HttpResult<string> ValidateAndNormalize(string value);
}

public interface ITenantSettingValueHandler : INamedStringValueHandler;

public interface IUserPreferenceValueHandler : INamedStringValueHandler;
