using System.Collections.Frozen;

namespace ControlR.Web.Server.Services.Settings;

internal static class SettingsExtensions
{
  public static FrozenDictionary<string, THandler> ToHandlerDictionary<THandler>(this IEnumerable<THandler> handlers)
    where THandler : INamedStringValueHandler
  {
    return handlers.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);
  }
}