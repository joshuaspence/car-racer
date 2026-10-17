using CarRacer.Core.Items;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Net
{
    /// <summary>A car's owner → host: "my car drove through this item box". The host grants the first claim (design doc §8).</summary>
    public sealed class ItemBoxClaimMessage : IGameMessage
    {
        public byte PlayerId;
        public ushort BoxIndex;

        public MessageType Type => MessageType.ItemBoxClaim;

        public void Write(NetWriter w)
        {
            w.WriteByte(PlayerId);
            w.WriteUShort(BoxIndex);
        }

        public static ItemBoxClaimMessage Read(NetReader r) => new ItemBoxClaimMessage
        {
            PlayerId = r.ReadByte(),
            BoxIndex = r.ReadUShort(),
        };
    }

    /// <summary>Host → all: a box was taken by <see cref="PlayerId"/>, who now holds <see cref="Item"/>; it comes back at <see cref="RespawnAtHostMs"/> in the host's clock.</summary>
    public sealed class ItemBoxTakenMessage : IGameMessage
    {
        public ushort BoxIndex;
        public byte PlayerId;
        public ItemKind Item;
        public long RespawnAtHostMs;

        public MessageType Type => MessageType.ItemBoxTaken;

        public void Write(NetWriter w)
        {
            w.WriteUShort(BoxIndex);
            w.WriteByte(PlayerId);
            w.WriteByte((byte)Item);
            w.WriteLong(RespawnAtHostMs);
        }

        public static ItemBoxTakenMessage Read(NetReader r) => new ItemBoxTakenMessage
        {
            BoxIndex = r.ReadUShort(),
            PlayerId = r.ReadByte(),
            Item = (ItemKind)r.ReadByte(),
            RespawnAtHostMs = r.ReadLong(),
        };
    }

    /// <summary>
    /// A car's owner → host: "I used my item here". If the player really holds it, the host stamps
    /// <see cref="UsedAtHostMs"/> and relays it to everyone so they can show it (design doc §8).
    /// </summary>
    public sealed class ItemUsedMessage : IGameMessage
    {
        public byte PlayerId;
        public ItemKind Item;
        public Vec3 Position;
        public Vec3 Velocity;
        public float HeadingRadians;

        /// <summary>Set by the host when it relays the message; 0 from the owner.</summary>
        public long UsedAtHostMs;

        public MessageType Type => MessageType.ItemUsed;

        public void Write(NetWriter w)
        {
            w.WriteByte(PlayerId);
            w.WriteByte((byte)Item);
            w.WriteVec3(Position);
            w.WriteVec3(Velocity);
            w.WriteFloat(HeadingRadians);
            w.WriteLong(UsedAtHostMs);
        }

        public static ItemUsedMessage Read(NetReader r) => new ItemUsedMessage
        {
            PlayerId = r.ReadByte(),
            Item = (ItemKind)r.ReadByte(),
            Position = r.ReadVec3(),
            Velocity = r.ReadVec3(),
            HeadingRadians = r.ReadFloat(),
            UsedAtHostMs = r.ReadLong(),
        };
    }
}
