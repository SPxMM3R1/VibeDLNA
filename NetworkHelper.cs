using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FolderDlnaServer;

internal static class NetworkHelper
{
    public static IPAddress GetLocalIPv4Address()
    {
        var candidates = GetLocalIPv4Addresses();
        if (candidates.Count > 0)
        {
            return candidates[0];
        }

        return Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .FirstOrDefault(static address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            ?? IPAddress.Loopback;
    }

    public static IReadOnlyList<IPAddress> GetLocalIPv4Addresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(static nic =>
                nic.OperationalStatus == OperationalStatus.Up
                && nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                && nic.NetworkInterfaceType is not NetworkInterfaceType.Tunnel)
            .Select(static nic => new
            {
                Interface = nic,
                Properties = nic.GetIPProperties()
            })
            .SelectMany(static item => item.Properties.UnicastAddresses
                .Where(static address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address.Address)
                    && !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                .Select(address => new
                {
                    address.Address,
                    HasGateway = item.Properties.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                }))
            .OrderByDescending(static item => item.HasGateway)
            .Select(static item => item.Address)
            .Distinct()
            .ToList();
}
