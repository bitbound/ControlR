namespace ControlR.Agent.Models;

internal class VncSession(Guid sessionId, Func<Task>? cleanupFunc = null) : IAsyncDisposable
{
    public Func<Task>? CleanupFunc { get; init; } = cleanupFunc;
    public Guid SessionId { get; } = sessionId;

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (CleanupFunc is not null)
            {
                await CleanupFunc.Invoke();
            }
        }
        catch { }
    }
}