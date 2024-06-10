using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.DevicesCommon.Services;

public interface IWakeOnLanService
{
    Task WakeDevice(string macAddress);

    Task WakeDevices(string[] macAddresses);
}

public class WakeOnLanService(ILogger<WakeOnLanService> _logger) : IWakeOnLanService
{
    public async Task WakeDevice(string macAddress)
    {
        try
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

            var broadcastAddress = IPAddress.Parse("255.255.255.255");
            var endpoint = new IPEndPoint(broadcastAddress, 9);
            await client.SendAsync(packet, packet.Length, endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while attempting to wake device with MAC address {MacAddress}.",
                macAddress);
        }
    }

    public async Task WakeDevices(string[] macAddresses)
    {
        foreach (var address in macAddresses)
        {
            await WakeDevice(address);
        }
    }
}