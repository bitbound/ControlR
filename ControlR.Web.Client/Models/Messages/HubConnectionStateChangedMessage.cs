using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Models.Messages;

public record HubConnectionStateChangedMessage(HubConnectionState NewState);
