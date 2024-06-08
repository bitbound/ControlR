using System.Text.Json;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Libraries.Shared.Extensions;

public static class GenericExtensions
{
    public static T Apply<T>(this T self, Action<T> action)
    {
        action(self);
        return self;
    }

    public static Task<T> AsTaskResult<T>(this T result)
    {
        return Task.FromResult(result);
    }

    public static Result<TTo> TryCloneAs<TFrom, TTo>(this TFrom value)
    {
        try
        {
            var converted = JsonSerializer.Deserialize<TTo>(JsonSerializer.Serialize(value));
            if (converted is not null)
            {
                return Result.Ok(converted);
            }

            return Result.Fail<TTo>("Serialization failure.");
        }
        catch (Exception ex)
        {
            return Result.Fail<TTo>(ex);
        }
    }
}
