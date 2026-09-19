namespace ControlR.Web.Server.Hubs;

/// <summary>
/// Lifetimes for cached hub stream sessions. Every <c>GetOrCreate</c> call site names one.
/// </summary>
public static class HubStreamExpiration
{
  /// <summary>
  /// A preview is a short frame push that the browser asks for again, so a leftover session should not linger.
  /// </summary>
  public static readonly TimeSpan DesktopPreview = TimeSpan.FromMinutes(5);

  /// <summary>
  /// A transfer runs until the bytes are across the wire, which for a large file takes a while.
  /// </summary>
  public static readonly TimeSpan FileTransfer = TimeSpan.FromMinutes(30);

  /// <summary>
  /// A directory listing arrives in one burst once the agent answers, so it drains fast.
  /// </summary>
  public static readonly TimeSpan Listing = TimeSpan.FromMinutes(5);
}
