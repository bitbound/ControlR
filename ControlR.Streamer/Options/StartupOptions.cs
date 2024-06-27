namespace ControlR.Streamer.Options;

public class StartupOptions
{
    private Uri? _serverOrigin;
    public string AuthorizedKey { get; set; } = string.Empty;
    public bool NotifyUser { get; set; }

    public Uri ServerOrigin
    {
        get => _serverOrigin ?? throw new InvalidOperationException("ServerOrigin hasn't been set yet.");
        set => _serverOrigin = value;
    }

    public Guid SessionId { get; set; }
    public string ViewerConnectionId { get; set; } = string.Empty;

    public string? ViewerName { get; set; }
}
