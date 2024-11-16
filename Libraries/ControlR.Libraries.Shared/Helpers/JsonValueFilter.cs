using System.Text.Json;

namespace ControlR.Libraries.Shared.Helpers;
public static class JsonValueFilter
{
  public static Func<T, bool> GetQuickFilter<T>(
    string searchText,
    ILogger logger)
  {

    return x =>
    {
      if (string.IsNullOrWhiteSpace(searchText))
      {
        return true;
      }

      var element = JsonSerializer.SerializeToElement(x);
      foreach (var property in element.EnumerateObject())
      {
        try
        {
          if (property.Value.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase))
          {
            return true;
          }
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error while filtering items of type {Type}.", typeof(T).Name);
        }
      }

      return false;
    };
  }
}
