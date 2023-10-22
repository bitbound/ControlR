namespace ControlR.Agent.Models;

internal class VncSession(Guid sessionId, Func<Task> cleanupFunc) : IAsyncDisposable
{
    public Func<Task> CleanupFunc { get; init; } = cleanupFunc;
    public Guid SessionId { get; } = sessionId;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CleanupFunc.Invoke();
        }
        catch { }
    }
}