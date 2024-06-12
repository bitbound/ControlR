using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Viewer.Models.Messages;

public record HubConnectionStateChangedMessage(HubConnectionState NewState);
