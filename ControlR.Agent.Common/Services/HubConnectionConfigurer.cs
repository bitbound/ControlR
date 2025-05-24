using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Net;

namespace ControlR.Agent.Common.Services;

public interface IHubConnectionConfigurer
{
  void ConfigureHubConnection(HttpConnectionOptions httpConnectionOptions);
}

internal class AgentHubConnectionConfigurer : IHubConnectionConfigurer
{
  public void ConfigureHubConnection(HttpConnectionOptions httpConnectionOptions)
  {
    httpConnectionOptions.SkipNegotiation = true;
    httpConnectionOptions.Transports = HttpTransportType.WebSockets;
  }
}

internal class LoadTestHubConnectionConfigurer(
  ILogger<LoadTestHubConnectionConfigurer> logger) : IHubConnectionConfigurer
{
  private readonly ILogger<LoadTestHubConnectionConfigurer> _logger = logger;
  private const int SO_REUSEPORT = 15;
  private static int _connectionCount;
  private static int[] _lastOctets = [.. Enumerable.Range(149, 5)];
  private static int[] _ports = [.. Enumerable.Range(32_768, 28_231)];


  public void ConfigureHubConnection(HttpConnectionOptions httpConnectionOptions)
  {
    ArgumentNullException.ThrowIfNull(httpConnectionOptions.Url);
    httpConnectionOptions.SkipNegotiation = true;
    httpConnectionOptions.Transports = HttpTransportType.WebSockets;

    var messageInvoker = GetMessageInvoker();

    httpConnectionOptions.WebSocketFactory = async (ctx, cancellationToken) =>
    {
      var webSocket = new ClientWebSocket();
      var wsUri = httpConnectionOptions.Url.ToWebsocketUri();
      await webSocket.ConnectAsync(wsUri, messageInvoker, cancellationToken);

      return webSocket;
    };
  }

  private HttpMessageInvoker GetMessageInvoker()
  {
    var socketsHandler = new SocketsHttpHandler()
    {

      ConnectCallback = async (context, token) =>
      {
        while (true)
        {
          try
          {
            var connectionCount = Interlocked.Increment(ref _connectionCount) - 1;
            var lastOctet = _lastOctets[connectionCount % _lastOctets.Length];
            var port = _ports[connectionCount % _ports.Length];
            var localEndpoint = new IPEndPoint(IPAddress.Parse($"192.168.0.{lastOctet}"), port);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)SO_REUSEPORT, true);
            socket.Bind(localEndpoint);
            var remoteEndpoint = new IPEndPoint(IPAddress.Loopback, context.DnsEndPoint.Port);
            await socket.ConnectAsync(remoteEndpoint, token);
            return new NetworkStream(socket, ownsSocket: false);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to get bound socket.");
          }
        }
      },
    };

    return new HttpMessageInvoker(socketsHandler);
  }

}