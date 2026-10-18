using System.Collections.Generic;

namespace CarRacer.Core.Net
{
    /// <summary>Guest → host, first message after connecting: who I am and which protocol I speak.</summary>
    public sealed class JoinRequestMessage : IGameMessage
    {
        public ushort ProtocolVersion;
        public string PlayerName = string.Empty;

        /// <summary>The car body chosen in the garage (design doc §11).</summary>
        public byte BodyIndex;

        /// <summary>The colour chosen in the garage; the host gives the nearest free one if it's taken.</summary>
        public byte ColourIndex;

        public MessageType Type => MessageType.JoinRequest;

        public void Write(NetWriter w)
        {
            w.WriteUShort(ProtocolVersion);
            w.WriteString(PlayerName);
            w.WriteByte(BodyIndex);
            w.WriteByte(ColourIndex);
        }

        public static JoinRequestMessage Read(NetReader r)
        {
            // The version comes first, so a guest on another version is still told why it can't join.
            var message = new JoinRequestMessage { ProtocolVersion = r.ReadUShort(), PlayerName = r.ReadString() };
            if (r.Remaining >= 2)
            {
                message.BodyIndex = r.ReadByte();
                message.ColourIndex = r.ReadByte();
            }

            return message;
        }
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

        /// <summary>A car the host drives (design doc §9), shown as "(bot)".</summary>
        public bool IsBot;

        /// <summary>Which car body (design doc §11).</summary>
        public byte BodyIndex;

        /// <summary>The track this player voted for (design doc §11), or <see cref="VoteTrackMessage.NoVote"/>.</summary>
        public byte TrackVote;
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
                w.WriteBool(p.IsBot);
                w.WriteByte(p.BodyIndex);
                w.WriteByte(p.TrackVote);
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
                    IsBot = r.ReadBool(),
                    BodyIndex = r.ReadByte(),
                    TrackVote = r.ReadByte(),
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

    /// <summary>Guest → host: the guest's vote for the next track (design doc §11), or <see cref="NoVote"/> to withdraw it.</summary>
    public sealed class VoteTrackMessage : IGameMessage
    {
        public const byte NoVote = byte.MaxValue;

        public byte TrackIndex = NoVote;

        public MessageType Type => MessageType.VoteTrack;

        public void Write(NetWriter w) => w.WriteByte(TrackIndex);

        public static VoteTrackMessage Read(NetReader r) => new VoteTrackMessage { TrackIndex = r.ReadByte() };
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
