using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CarRacer.Core.Discovery
{
    /// <summary>
    /// Joining by typing the host's address, for networks where discovery packets are blocked
    /// (design doc §1.1): parsing what the guest typed, and finding this phone's own addresses
    /// so the host can show them.
    /// </summary>
    public static class HostAddress
    {
        /// <summary>
        /// Reads "192.168.1.23" or "192.168.1.23:47778" (spaces around it are ignored). Only IPv4
        /// dotted quads are accepted, since that's what a phone's Wi-Fi settings show; without a
        /// port the default game port is used.
        /// </summary>
        public static bool TryParse(string text, out string address, out ushort port)
        {
            address = null;
            port = DiscoveryConstants.DefaultGamePort;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            string host = trimmed;
            int colon = trimmed.IndexOf(':');
            if (colon >= 0)
            {
                host = trimmed.Substring(0, colon);
                if (!ushort.TryParse(trimmed.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port == 0)
                {
                    return false;
                }
            }

            string[] parts = host.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 3 || !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    return false;
                }
            }

            address = host;
            return true;
        }

        /// <summary>
        /// This device's IPv4 addresses on its networks (not loopback), for the host to show. Interface
        /// enumeration is restricted on some Android versions, so it falls back to asking which address
        /// the default route would use; that sends no packets.
        /// </summary>
        public static IReadOnlyList<string> LocalIPv4Addresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        Add(result, unicast.Address);
                    }
                }
            }
            catch (Exception)
            {
                // NetworkInformationException, NotImplementedException, etc. on restricted platforms.
            }

            if (result.Count == 0)
            {
                try
                {
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        // Connecting a UDP socket only picks a route and local address; nothing is sent.
                        socket.Connect(new IPEndPoint(IPAddress.Parse("10.255.255.255"), DiscoveryConstants.DefaultGamePort));
                        Add(result, ((IPEndPoint)socket.LocalEndPoint).Address);
                    }
                }
                catch (SocketException)
                {
                    // No network at all.
                }
            }

            return result;
        }

        private static void Add(List<string> result, IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any))
            {
                return;
            }

            string text = address.ToString();
            if (!result.Contains(text))
            {
                result.Add(text);
            }
        }
    }
}
