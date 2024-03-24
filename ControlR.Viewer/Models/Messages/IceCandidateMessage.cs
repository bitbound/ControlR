namespace ControlR.Viewer.Models.Messages;
internal class IceCandidateMessage(Guid sessionId, string candidateJson)
{
    public Guid SessionId { get; } = sessionId;
    public string CandidateJson { get; } = candidateJson;
}
