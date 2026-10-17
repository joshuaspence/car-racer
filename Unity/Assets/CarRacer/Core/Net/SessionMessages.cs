using System.Collections.Generic;

namespace CarRacer.Core.Net
{
    /// <summary>Guest → host, first message after connecting: who I am and which protocol I speak.</summary>
    public sealed class JoinRequestMessage : IGameMessage
    {
        public ushort ProtocolVersion;
        public string PlayerName = string.Empty;

        public MessageType Type => MessageType.JoinRequest;

        public void Write(NetWriter w)
        {
            w.WriteUShort(ProtocolVersion);
            w.WriteString(PlayerName);
        }

        public static JoinRequestMessage Read(NetReader r) => new JoinRequestMessage
        {
            ProtocolVersion = r.ReadUShort(),
            PlayerName = r.ReadString(),
        };
    }

    public enum JoinResult : byte
    {
        Accepted = 0,
        SessionFull = 1,
        VersionMismatch = 2,
        EventInProgress = 3,
    }

    /// <summary>Host → guest: accepted with a player id, or refused (the host then disconnects the guest).</summary>
    public sealed class JoinReplyMessage : IGameMessage
    {
        public JoinResult Result;
        public byte PlayerId;

        public MessageType Type => MessageType.JoinReply;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Result);
            w.WriteByte(PlayerId);
        }

        public static JoinReplyMessage Read(NetReader r) => new JoinReplyMessage
        {
            Result = (JoinResult)r.ReadByte(),
            PlayerId = r.ReadByte(),
        };
    }

    public struct RosterEntry
    {
        public byte PlayerId;
        public string Name;
        public byte ColourIndex;
        public bool Ready;
        public bool IsHost;
    }

    /// <summary>Host → all, whenever anyone joins, leaves or changes: the full player list.</summary>
    public sealed class RosterMessage : IGameMessage
    {
        public List<RosterEntry> Players = new List<RosterEntry>();

        public MessageType Type => MessageType.Roster;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Players.Count);
            foreach (var p in Players)
            {
                w.WriteByte(p.PlayerId);
                w.WriteString(p.Name);
                w.WriteByte(p.ColourIndex);
                w.WriteBool(p.Ready);
                w.WriteBool(p.IsHost);
            }
        }

        public static RosterMessage Read(NetReader r)
        {
            int count = r.ReadByte();
            var message = new RosterMessage();
            for (int i = 0; i < count; i++)
            {
                message.Players.Add(new RosterEntry
                {
                    PlayerId = r.ReadByte(),
                    Name = r.ReadString(),
                    ColourIndex = r.ReadByte(),
                    Ready = r.ReadBool(),
                    IsHost = r.ReadBool(),
                });
            }

            return message;
        }
    }

    /// <summary>Guest → host: a clock sample request, stamped with the guest's clock (see <see cref="ClockSync"/>).</summary>
    public sealed class ClockPingMessage : IGameMessage
    {
        public long GuestSentMs;

        public MessageType Type => MessageType.ClockPing;

        public void Write(NetWriter w) => w.WriteLong(GuestSentMs);

        public static ClockPingMessage Read(NetReader r) => new ClockPingMessage { GuestSentMs = r.ReadLong() };
    }

    /// <summary>Host → guest: the ping's own timestamp echoed back with the host's clock at reply time.</summary>
    public sealed class ClockPongMessage : IGameMessage
    {
        public long GuestSentMs;
        public long HostMs;

        public MessageType Type => MessageType.ClockPong;

        public void Write(NetWriter w)
        {
            w.WriteLong(GuestSentMs);
            w.WriteLong(HostMs);
        }

        public static ClockPongMessage Read(NetReader r) => new ClockPongMessage
        {
            GuestSentMs = r.ReadLong(),
            HostMs = r.ReadLong(),
        };
    }

    /// <summary>Guest → host: the guest tapped Ready (or un-Ready) in the lobby.</summary>
    public sealed class SetReadyMessage : IGameMessage
    {
        public bool Ready;

        public MessageType Type => MessageType.SetReady;

        public void Write(NetWriter w) => w.WriteBool(Ready);

        public static SetReadyMessage Read(NetReader r) => new SetReadyMessage { Ready = r.ReadBool() };
    }
}
