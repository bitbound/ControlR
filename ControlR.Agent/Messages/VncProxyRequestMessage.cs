namespace ControlR.Agent.Messages;
internal record VncProxyRequestMessage(Guid SessionId, int? VncProcessId = null);