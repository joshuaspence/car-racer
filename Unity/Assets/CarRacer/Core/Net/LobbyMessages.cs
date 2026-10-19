using CarRacer.Core.Session;

namespace CarRacer.Core.Net
{
    /// <summary>
    /// Host → all, when anything changes and to each guest as it joins: the next event's settings
    /// (design doc §11), and the cup saved on the host, if any, so the room leader can resume it.
    /// </summary>
    public sealed class LobbySettingsMessage : IGameMessage
    {
        public LobbySettings Settings = LobbySettings.Default;

        /// <summary>The saved cup, as <see cref="LobbySettings.Cup"/> (0 for none), and how many of its events are done.</summary>
        public byte SavedCup;
        public byte SavedCupDone;

        public MessageType Type => MessageType.LobbySettings;

        public void Write(NetWriter w)
        {
            Settings.Write(w);
            w.WriteByte(SavedCup);
            w.WriteByte(SavedCupDone);
        }

        public static LobbySettingsMessage Read(NetReader r) => new LobbySettingsMessage
        {
            Settings = LobbySettings.Read(r),
            SavedCup = r.ReadByte(),
            SavedCupDone = r.ReadByte(),
        };
    }

    /// <summary>What the room leader can ask the host to do (design doc §2.3): what the host's own lobby and results buttons do.</summary>
    public enum LobbyCommand : byte
    {
        /// <summary>Use the settings that come with the command.</summary>
        ChangeSettings = 0,

        /// <summary>Start the single event or the cup the settings say.</summary>
        Start = 1,

        ResumeSavedCup = 2,
        PauseCup = 3,
        UnpauseCup = 4,

        /// <summary>Skip the wait before the cup's next event.</summary>
        NextEvent = 5,

        /// <summary>Leave the cup for the lobby, keeping its save.</summary>
        LeaveCup = 6,

        /// <summary>From the results (or a won cup's table) back to the lobby.</summary>
        BackToLobby = 7,
    }

    /// <summary>Guest → host: the room leader's command; the host ignores it from anyone else.</summary>
    public sealed class LobbyCommandMessage : IGameMessage
    {
        public LobbyCommand Command;

        /// <summary>The new settings, for <see cref="LobbyCommand.ChangeSettings"/>; otherwise unused.</summary>
        public LobbySettings Settings = LobbySettings.Default;

        public MessageType Type => MessageType.LobbyCommand;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Command);
            Settings.Write(w);
        }

        public static LobbyCommandMessage Read(NetReader r)
        {
            byte command = r.ReadByte();
            if (command > (byte)LobbyCommand.BackToLobby)
            {
                throw new NetFormatException($"Unknown lobby command {command}.");
            }

            return new LobbyCommandMessage { Command = (LobbyCommand)command, Settings = LobbySettings.Read(r) };
        }
    }
}
