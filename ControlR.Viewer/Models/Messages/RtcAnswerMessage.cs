namespace ControlR.Viewer.Models.Messages;
internal class RtcSessionDescriptionMessage(Guid sessionId, RtcSessionDescription sessionDescription)
{
    public Guid SessionId { get; } = sessionId;
    public RtcSessionDescription SessionDescription { get; } = sessionDescription;
}
