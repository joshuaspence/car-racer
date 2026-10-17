using System.Collections.Generic;

namespace CarRacer.Core.Net
{
    public struct StandingsEntry
    {
        public byte PlayerId;
        public byte LapsCompleted;

        /// <summary>1-based finishing position, or 0 while still racing.</summary>
        public byte FinishPosition;
    }

    /// <summary>Host → all, a couple of times a second during a race: everyone in race order, with laps done.</summary>
    public sealed class StandingsMessage : IGameMessage
    {
        public List<StandingsEntry> Order = new List<StandingsEntry>();

        public MessageType Type => MessageType.Standings;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Order.Count);
            foreach (var e in Order)
            {
                w.WriteByte(e.PlayerId);
                w.WriteByte(e.LapsCompleted);
                w.WriteByte(e.FinishPosition);
            }
        }

        public static StandingsMessage Read(NetReader r)
        {
            int count = r.ReadByte();
            var message = new StandingsMessage();
            for (int i = 0; i < count; i++)
            {
                message.Order.Add(new StandingsEntry { PlayerId = r.ReadByte(), LapsCompleted = r.ReadByte(), FinishPosition = r.ReadByte() });
            }

            return message;
        }
    }

    public struct RaceResultEntry
    {
        public byte PlayerId;

        /// <summary>The name at the start of the race, so a player who has since left still shows by name.</summary>
        public string Name;
        public byte Position;
        public long RaceTimeMs;
        public bool DidNotFinish;
    }

    /// <summary>Host → all: the race is over. Everyone in finishing order; unfinished racers are placed by race order and marked DNF.</summary>
    public sealed class RaceResultsMessage : IGameMessage
    {
        public List<RaceResultEntry> Results = new List<RaceResultEntry>();

        public MessageType Type => MessageType.RaceResults;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Results.Count);
            foreach (var e in Results)
            {
                w.WriteByte(e.PlayerId);
                w.WriteString(e.Name);
                w.WriteByte(e.Position);
                w.WriteLong(e.RaceTimeMs);
                w.WriteBool(e.DidNotFinish);
            }
        }

        public static RaceResultsMessage Read(NetReader r)
        {
            int count = r.ReadByte();
            var message = new RaceResultsMessage();
            for (int i = 0; i < count; i++)
            {
                message.Results.Add(new RaceResultEntry
                {
                    PlayerId = r.ReadByte(),
                    Name = r.ReadString(),
                    Position = r.ReadByte(),
                    RaceTimeMs = r.ReadLong(),
                    DidNotFinish = r.ReadBool(),
                });
            }

            return message;
        }
    }

    /// <summary>Host → all: results are done; everyone goes back to the same lobby for a rematch (design doc §11).</summary>
    public sealed class ReturnToLobbyMessage : IGameMessage
    {
        public MessageType Type => MessageType.ReturnToLobby;

        public void Write(NetWriter w)
        {
        }
    }
}
