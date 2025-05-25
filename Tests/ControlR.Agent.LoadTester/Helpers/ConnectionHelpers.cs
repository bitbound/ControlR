using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using ControlR.Agent.Common.Models;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Services;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace ControlR.Agent.LoadTester.Helpers;

public static class ConnectionHelper
{
  private const int SO_REUSEPORT = 15;
  private static int[] _lastOctets = [.. Enumerable.Range(200, 10)];
  private static int[] _ports = [.. Enumerable.Range(32_768, 28_231)];

  public static void ConfigureHubConnection(int agentNum, HttpConnectionOptions httpConnectionOptions)
  {
    ArgumentNullException.ThrowIfNull(httpConnectionOptions.Url);
    httpConnectionOptions.SkipNegotiation = true;
    httpConnectionOptions.Transports = HttpTransportType.WebSockets;

    var messageInvoker = GetMessageInvoker(agentNum);

    httpConnectionOptions.WebSocketFactory = async (ctx, cancellationToken) =>
    {
      var webSocket = new ClientWebSocket();
      var wsUri = httpConnectionOptions.Url.ToWebsocketUri();
      await webSocket.ConnectAsync(wsUri, messageInvoker, cancellationToken);

      return webSocket;
    };
  }



  public static HttpMessageInvoker GetMessageInvoker(int agentNum)
  {
    var socketsHandler = new SocketsHttpHandler()
    {

      ConnectCallback = async (context, token) =>
      {
        while (true)
        {
          try
          {
            var octetIndex = agentNum / _ports.Length % _lastOctets.Length;
            var portIndex = agentNum % _ports.Length;
            var lastOctet = _lastOctets[octetIndex];
            var port = _ports[portIndex];
            var localEndpoint = new IPEndPoint(IPAddress.Parse($"192.168.0.{lastOctet}"), port);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.SetRawSocketOption(1, SO_REUSEPORT, BitConverter.GetBytes(1));
            socket.Bind(localEndpoint);
            //var remoteEndpoint = new IPEndPoint(IPAddress.Loopback, context.DnsEndPoint.Port);
            var ips = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            var remoteEndpoint = new IPEndPoint(ips[0], context.DnsEndPoint.Port);
            await socket.ConnectAsync(remoteEndpoint, token);
            return new NetworkStream(socket, ownsSocket: false);
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Failed to get bound socket. Error: {ex}");
          }
        }
      },
    };

    return new HttpMessageInvoker(socketsHandler);
  }
  

  public static Task<DeviceDto> CreateDevice(
    Guid deviceId,
    Guid tenantId,
    int deviceNumber,
    Version agentVersion)
  {
    var totalMemory = Random.Shared.Next(4, 128);
    var usedMemory = Math.Clamp(totalMemory * Random.Shared.NextDouble(), 2, totalMemory - .25);
    var totalStorage = Random.Shared.Next(64, 4_000);
    var usedStorage = Math.Clamp(totalStorage * Random.Shared.NextDouble(), 30, totalStorage - .5);
    var cpuUtilization = Random.Shared.NextDouble();
    var currentUser = RandomGenerator.GenerateString(8);
    var osDrive = new Drive()
    {
      DriveFormat = "NTFS",
      DriveType = DriveType.Fixed,
      Name = "C:\\",
      TotalSize = totalStorage * 1_073_741_824, // Convert GB to bytes
      FreeSpace = totalStorage * 1_073_741_824 - usedStorage * 1_073_741_824, // Convert GB to bytes,
      RootDirectory = "C:\\",
      VolumeLabel = "OS",
    };

    var device = new DeviceModel
    {
      Id = deviceId,
      Name = $"Test Device {deviceNumber}",
      AgentVersion = $"{agentVersion}",
      TenantId = tenantId,
      IsOnline = true,
      Platform = SystemEnvironment.Instance.Platform,
      ProcessorCount = Environment.ProcessorCount,
      OsArchitecture = RuntimeInformation.OSArchitecture,
      OsDescription = RuntimeInformation.OSDescription,
      Is64Bit = Environment.Is64BitOperatingSystem,
      TotalMemory = totalMemory,
      UsedMemory = usedMemory,
      TotalStorage = totalStorage,
      UsedStorage = usedStorage,
      CpuUtilization = cpuUtilization,
      CurrentUsers = [currentUser],
      Drives = [osDrive],
    };

    var result = device.TryCloneAs<DeviceModel, DeviceDto>();
    if (!result.IsSuccess)
    {
      throw new InvalidOperationException($"Failed to clone device model: {result.Reason}");
    }
    return result.Value.AsTaskResult();
  }
}