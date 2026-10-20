using System.Collections.Generic;
using CarRacer.Core.Arena;

namespace CarRacer.Core.Scoring
{
    /// <summary>One event of a cup: a race on a track, or an arena.</summary>
    public struct CupEvent
    {
        public GameMode Mode;

        /// <summary>The track, for a race (an index into the game's track list).</summary>
        public int TrackIndex;

        public int Laps;

        public static CupEvent Race(int trackIndex, int laps = 3) => new CupEvent { Mode = GameMode.Race, TrackIndex = trackIndex, Laps = laps };

        public static CupEvent ArenaEvent(GameMode mode) => new CupEvent { Mode = mode, Laps = 1 };
    }

    /// <summary>A named series of events scored with a <see cref="Championship"/> (design doc §10).</summary>
    public sealed class Cup
    {
        public Cup(string id, string name, IReadOnlyList<CupEvent> events)
        {
            Id = id;
            Name = name;
            Events = events;
        }

        /// <summary>Stable id, for saves.</summary>
        public string Id { get; }

        public string Name { get; }
        public IReadOnlyList<CupEvent> Events { get; }
    }

    /// <summary>The built-in cups: Grand Prix (every track) and Mixed Cup (every track, then Sumo and Battle).</summary>
    public static class Cups
    {
        public static readonly Cup GrandPrix = new Cup("grand-prix", "Grand Prix", new[]
        {
            CupEvent.Race(0),
            CupEvent.Race(1),
            CupEvent.Race(2),
            CupEvent.Race(3),
            CupEvent.Race(4),
            CupEvent.Race(5),
            CupEvent.Race(6),
            CupEvent.Race(7),
        });

        public static readonly Cup MixedCup = new Cup("mixed", "Mixed Cup", new[]
        {
            CupEvent.Race(0),
            CupEvent.Race(1),
            CupEvent.Race(2),
            CupEvent.Race(3),
            CupEvent.Race(4),
            CupEvent.Race(5),
            CupEvent.Race(6),
            CupEvent.Race(7),
            CupEvent.ArenaEvent(GameMode.Sumo),
            CupEvent.ArenaEvent(GameMode.Battle),
        });

        /// <summary>Every built-in cup, in the order the lobby offers them.</summary>
        public static readonly IReadOnlyList<Cup> All = new[] { GrandPrix, MixedCup };

        /// <summary>The cup with id <paramref name="id"/>, or null.</summary>
        public static Cup Find(string id)
        {
            foreach (var cup in All)
            {
                if (cup.Id == id)
                {
                    return cup;
                }
            }

            return null;
        }
    }
}
