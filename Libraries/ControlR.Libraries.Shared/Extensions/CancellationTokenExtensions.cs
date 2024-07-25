namespace ControlR.Libraries.Shared.Extensions;

public static class CancellationTokenExtensions
{
    public static async Task WhenCancelled(this CancellationToken cancellationToken, CancellationToken extraCancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || extraCancellationToken.IsCancellationRequested)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, extraCancellationToken);
        var tcs = new TaskCompletionSource();
        linkedCts.Token.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }
}
