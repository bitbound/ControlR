namespace ControlR.Agent.Messages;
internal record VncRequestMessage(Guid SessionId, int Port, int VncProcessId);