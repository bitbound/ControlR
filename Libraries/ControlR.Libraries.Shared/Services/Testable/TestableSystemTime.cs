namespace ControlR.Libraries.Shared.Services.Testable;

public class TestableSystemTime : ISystemTime
{
    private TimeSpan _adjustBy;
    private DateTimeOffset? _setTime;
    public DateTimeOffset Now
    {
        get
        {
            var baseTime = _setTime ?? DateTimeOffset.Now;
            return baseTime + _adjustBy;
        }
    }

    public DateTimeOffset UtcNow => Now.ToUniversalTime();


    public DateTimeOffset AdjustBy(TimeSpan adjustBy)
    {
        _adjustBy = adjustBy;
        return Now;
    }

    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(endingTimestamp) - DateTimeOffset.FromUnixTimeMilliseconds(startingTimestamp);
    }

    public TimeSpan GetElapsedTime(long startingTimestamp)
    {
        return GetElapsedTime(startingTimestamp, Now.ToUnixTimeMilliseconds());
    }

    public long GetTimestamp()
    {
        return Now.ToUnixTimeMilliseconds();
    }

    public void Reset()
    {
        _setTime = null;
        _adjustBy = TimeSpan.Zero;
    }

    public void Set(DateTimeOffset time)
    {
        _setTime = time;
    }
}
