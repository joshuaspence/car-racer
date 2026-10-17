using System;
using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Session;

namespace CarRacer.Core.Scoring
{
    /// <summary>
    /// The host's cup in progress (design doc §10). It starts each event of the <see cref="Cup"/>
    /// on its <see cref="HostRace"/>, scores the results in a <see cref="Championship"/> (players
    /// are known by name, so a player who leaves and rejoins keeps their points), sends everyone the
    /// table, and starts the next event <see cref="AdvanceMs"/> later unless the host pauses. With
    /// <see cref="ReverseGrid"/> the leader starts last. <see cref="ToBytes"/> is the auto-save,
    /// taken after every event, and <see cref="Resume"/> picks a saved cup back up.
    /// </summary>
    public sealed class CupRun
    {
        /// <summary>Time on the standings screen before the next event starts by itself.</summary>
        public const long AdvanceMs = 10_000;

        private const ushort SaveFormatVersion = 1;

        private readonly HostSession _session;
        private readonly HostRace _race;
        private readonly Func<long> _clockMs;
        private bool _scoredThisEvent;

        public CupRun(HostSession session, HostRace race, Func<long> clockMs, Cup cup, bool reverseGrid)
            : this(session, race, clockMs, cup, reverseGrid, new Championship(cup.Events.Count))
        {
        }

        private CupRun(HostSession session, HostRace race, Func<long> clockMs, Cup cup, bool reverseGrid, Championship championship)
        {
            _session = session;
            _race = race;
            _clockMs = clockMs;
            Cup = cup;
            ReverseGrid = reverseGrid;
            Championship = championship;
        }

        public Cup Cup { get; }
        public bool ReverseGrid { get; }
        public Championship Championship { get; }

        /// <summary>The latest table sent to everyone; null before the first event ends.</summary>
        public CupStandingsMessage Standings { get; private set; }

        /// <summary>The event to run next (or running now), or null once the cup is over.</summary>
        public CupEvent? NextEvent => Championship.IsComplete ? (CupEvent?)null : Cup.Events[Championship.EventsCompleted];

        public bool IsComplete => Championship.IsComplete;

        /// <summary>Holds the standings screen instead of starting the next event after <see cref="AdvanceMs"/>.</summary>
        public bool Paused { get; private set; }

        /// <summary>When the next event starts, in host time; 0 while paused, during an event, or once the cup is over.</summary>
        public long NextEventAtMs { get; private set; }

        /// <summary>Raised after each event is scored, with the auto-save.</summary>
        public event Action<byte[]> Saved;

        /// <summary>Starts the next event from the lobby. Returns false if one is under way or the cup is over.</summary>
        public bool StartNextEvent()
        {
            var next = NextEvent;
            if (!next.HasValue || _race.Phase != RacePhase.Lobby)
            {
                return false;
            }

            var e = next.Value;
            if (!_race.Start(e.Laps, e.Mode, e.TrackIndex, GridRank()))
            {
                return false;
            }

            _scoredThisEvent = false;
            NextEventAtMs = 0;
            return true;
        }

        /// <summary>Call every frame on the host: scores an event once its results are in, and moves on when it's time.</summary>
        public void Tick()
        {
            if (_race.Phase == RacePhase.Results && !_scoredThisEvent && _race.Results != null)
            {
                Score(_race.Results);
            }

            if (_race.Phase == RacePhase.Results && _scoredThisEvent && !IsComplete && !Paused && NextEventAtMs > 0 && _clockMs() >= NextEventAtMs)
            {
                AdvanceNow();
            }
        }

        /// <summary>Pauses or resumes the countdown to the next event; resuming restarts the full countdown.</summary>
        public void SetPaused(bool paused)
        {
            if (paused == Paused)
            {
                return;
            }

            Paused = paused;
            if (_race.Phase == RacePhase.Results && _scoredThisEvent && !IsComplete)
            {
                NextEventAtMs = paused ? 0 : _clockMs() + AdvanceMs;
                Broadcast();
            }
        }

        /// <summary>The host skips the wait: back through the lobby and straight into the next event.</summary>
        public bool AdvanceNow()
        {
            if (IsComplete || !_scoredThisEvent || !_race.ReturnToLobby())
            {
                return false;
            }

            return StartNextEvent();
        }

        private void Score(RaceResultsMessage results)
        {
            var order = new List<string>();
            var sorted = new List<RaceResultEntry>(results.Results);
            sorted.Sort((a, b) => a.Position.CompareTo(b.Position));
            foreach (var result in sorted)
            {
                string key = Key(result.Name);
                if (order.Contains(key))
                {
                    continue;
                }

                Championship.AddPlayer(key, result.Name);
                order.Add(key);
            }

            Championship.RecordEvent(order);
            _scoredThisEvent = true;
            NextEventAtMs = IsComplete || Paused ? 0 : _clockMs() + AdvanceMs;
            Broadcast();
            Saved?.Invoke(ToBytes());
        }

        private void Broadcast()
        {
            var message = new CupStandingsMessage
            {
                CupName = Cup.Name,
                EventsCompleted = (byte)Championship.EventsCompleted,
                TotalEvents = (byte)Championship.TotalEvents,
                NextEventAtHostMs = NextEventAtMs,
            };
            foreach (var entry in Championship.Standings())
            {
                message.Standings.Add(new CupStandingsEntry
                {
                    Name = entry.DisplayName,
                    Points = (ushort)Math.Min(entry.Points, ushort.MaxValue),
                    Wins = (byte)Math.Min(entry.Wins, byte.MaxValue),
                    LastEventPosition = (byte)Math.Min(entry.LastEventPosition, byte.MaxValue),
                });
            }

            Standings = message;
            _session.Broadcast(message, reliable: true);
        }

        /// <summary>Grid rank by name: the championship order (reversed with <see cref="ReverseGrid"/>); newcomers start at the front with reverse grid, at the back without.</summary>
        private Func<string, int> GridRank()
        {
            var order = Championship.GridOrder(ReverseGrid);
            var rank = new Dictionary<string, int>();
            for (int i = 0; i < order.Count; i++)
            {
                rank[order[i]] = i;
            }

            int newcomer = ReverseGrid ? -1 : order.Count;
            return name => rank.TryGetValue(Key(name), out int r) ? r : newcomer;
        }

        /// <summary>Players are known by their name, ignoring case and spaces at the ends.</summary>
        private static string Key(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

        public byte[] ToBytes()
        {
            var w = new NetWriter();
            w.WriteUShort(SaveFormatVersion);
            w.WriteString(Cup.Id);
            w.WriteBool(ReverseGrid);
            var championship = Championship.ToBytes();
            w.WriteUShort((ushort)championship.Length);
            w.WriteBytes(championship);
            return w.ToArray();
        }

        /// <summary>A saved cup, back in the lobby ready for its next event; null if the save is unreadable or for a cup this build doesn't have.</summary>
        public static CupRun Resume(byte[] save, HostSession session, HostRace race, Func<long> clockMs)
        {
            try
            {
                var r = new NetReader(save);
                if (r.ReadUShort() != SaveFormatVersion)
                {
                    return null;
                }

                var cup = Cups.Find(r.ReadString());
                bool reverseGrid = r.ReadBool();
                var championship = Championship.FromBytes(r.ReadBytes(r.ReadUShort()));
                if (cup == null || championship.TotalEvents != cup.Events.Count || championship.IsComplete)
                {
                    return null;
                }

                return new CupRun(session, race, clockMs, cup, reverseGrid, championship);
            }
            catch (NetFormatException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
