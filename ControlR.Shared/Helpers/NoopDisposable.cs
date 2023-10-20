namespace ControlR.Shared.Helpers;

public class NoopDisposable : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
