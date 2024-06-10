namespace ControlR.Libraries.Shared.Services;

public interface ISystemTime
{
    DateTimeOffset Now { get; }

    DateTimeOffset UtcNow { get; }

    TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);

    TimeSpan GetElapsedTime(long startingTimestamp);

    long GetTimestamp();
}

public class SystemTime : TimeProvider, ISystemTime
{
    public DateTimeOffset Now => GetLocalNow();
    public DateTimeOffset UtcNow => GetUtcNow();
}