using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Devices.Common.Services;
public interface IWakeOnLanService
{
    Task WakeDevice(string macAddress);
}

public class WakeOnLanService : IWakeOnLanService
{
    public async Task WakeDevice(string macAddress)
    {
        var macBytes = Convert.FromHexString(macAddress);

        using var client = new UdpClient();

        var macData = Enumerable
            .Repeat(macBytes, 16)
            .SelectMany(x => x);

        var packet = Enumerable
            .Repeat((byte)0xFF, 6)
            .Concat(macData)
            .ToArray();

        var broadcastAddress = System.Net.IPAddress.Parse("255.255.255.255");
        var endpoint = new IPEndPoint(broadcastAddress, 9);
        await client.SendAsync(packet, packet.Length, endpoint);
    }
}
