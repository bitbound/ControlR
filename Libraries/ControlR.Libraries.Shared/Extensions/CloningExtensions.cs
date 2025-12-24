using System.Text.Json;

namespace ControlR.Libraries.Shared.Extensions;

public static class CloningExtensions
{
  public static TToType CloneAs<TToType>(this object value)
  {
    return JsonSerializer.Deserialize<TToType>(JsonSerializer.Serialize(value)) ??
      throw new JsonException("Failed to clone object via JsonSerializer.");
  }

  public static Result<TToType> TryCloneAs<TToType>(this object value)
  {
    try
    {
      var converted = JsonSerializer.Deserialize<TToType>(JsonSerializer.Serialize(value));
      if (converted is not null)
      {
        return Result.Ok(converted);
      }

      return Result.Fail<TToType>("Serialization failure.");
    }
    catch (Exception ex)
    {
      return Result.Fail<TToType>(ex);
    }
  }
}