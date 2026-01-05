using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Libraries.Viewer.Common.Models.Messages;

public record HubConnectionStateChangedMessage(HubConnectionState NewState);
