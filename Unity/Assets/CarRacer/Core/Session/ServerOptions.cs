using System;
using System.Collections.Generic;
using System.Globalization;
using CarRacer.Core.Discovery;

namespace CarRacer.Core.Session
{
    /// <summary>
    /// The command line of a dedicated host (design doc §2.3): <c>-server</c> to host with no
    /// player (the Linux server build always does), <c>-port</c> for the game port, and
    /// <c>-name</c> for the name phones list it under. Unity's own arguments are ignored.
    /// </summary>
    public sealed class ServerOptions
    {
        public const string ServerFlag = "-server";
        public const string PortFlag = "-port";
        public const string NameFlag = "-name";
        public const string DefaultName = "Car Racer server";

        public bool IsServer { get; private set; }
        public ushort Port { get; private set; } = DiscoveryConstants.DefaultGamePort;
        public string Name { get; private set; } = DefaultName;

        /// <summary>Why the command line couldn't be used, or null if it could.</summary>
        public string Error { get; private set; }

        public static ServerOptions Parse(IReadOnlyList<string> args)
        {
            var options = new ServerOptions();
            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, ServerFlag, StringComparison.OrdinalIgnoreCase))
                {
                    options.IsServer = true;
                }
                else if (string.Equals(arg, PortFlag, StringComparison.OrdinalIgnoreCase))
                {
                    string value = i + 1 < args.Count ? args[++i] : string.Empty;
                    if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port == 0 || port == DiscoveryConstants.DiscoveryPort)
                    {
                        options.Error = $"{PortFlag} needs a port from 1 to 65535 other than {DiscoveryConstants.DiscoveryPort} (used for discovery), not \"{value}\"";
                    }
                    else
                    {
                        options.Port = port;
                    }
                }
                else if (string.Equals(arg, NameFlag, StringComparison.OrdinalIgnoreCase))
                {
                    options.Name = LobbyRoster.CleanName(i + 1 < args.Count ? args[++i] : string.Empty);
                }
            }

            return options;
        }
    }
}
