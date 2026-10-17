using System;
using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Core.Session;

namespace CarRacer.Core.Race
{
    public enum RacePhase
    {
        Lobby,
        Countdown,
        Racing,
        Results,
    }

    /// <summary>What the race screen needs, the same on the host and on guests.</summary>
    public interface IRaceView
    {
        RacePhase Phase { get; }
        int LapCount { get; }

        /// <summary>When the race starts (or started), in this phone's clock.</summary>
        long StartAtLocalMs { get; }

        /// <summary>Latest race order from the host; null before the first update of a race.</summary>
        StandingsMessage Standings { get; }

        /// <summary>Final results; null until the race ends.</summary>
        RaceResultsMessage Results { get; }

        /// <summary>Call every frame.</summary>
        void Tick();

        /// <summary>This phone's car crossed checkpoint <paramref name="index"/> (from <see cref="CheckpointDetector"/>).</summary>
        void ReportOwnCheckpoint(int index);
    }

    /// <summary>
    /// The host's race referee (design doc §2, §7.1): starts a synced countdown, validates every
    /// player's checkpoints with a <see cref="RaceTracker"/>, announces finishers, broadcasts
    /// standings, ends the race when everyone is home or 30 s after the winner, and reopens the lobby.
    /// </summary>
    public sealed class HostRace : IRaceView
    {
        /// <summary>The 3-2-1 countdown.</summary>
        public const long CountdownMs = 3000;

        /// <summary>Extra time before the countdown so the start message reaches every guest first.</summary>
        public const long StartLeadMs = 500;

        /// <summary>Once someone finishes, the others have this long before the race ends without them.</summary>
        public const long PostFinishMs = 30000;

        public const long StandingsIntervalMs = 500;

        private readonly HostSession _session;
        private readonly int _checkpointCount;
        private readonly Func<long> _clockMs;
        private readonly Func<int, float> _fractionToNextCheckpoint;
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private long _firstFinishMs = -1;
        private long _nextStandingsMs;

        /// <param name="fractionToNextCheckpoint">How far (0..1) a player's car is towards its next checkpoint, from track geometry.</param>
        public HostRace(HostSession session, int checkpointCount, Func<long> clockMs, Func<int, float> fractionToNextCheckpoint)
        {
            _session = session;
            _checkpointCount = checkpointCount;
            _clockMs = clockMs;
            _fractionToNextCheckpoint = fractionToNextCheckpoint;
            _session.CheckpointReceived += OnCheckpoint;
            _session.PlayerLeft += OnPlayerLeft;
        }

        public RacePhase Phase { get; private set; } = RacePhase.Lobby;
        public int LapCount { get; private set; }
        public long StartAtLocalMs { get; private set; }
        public StandingsMessage Standings { get; private set; }
        public RaceResultsMessage Results { get; private set; }
        public RaceTracker Tracker { get; private set; }

        /// <summary>Starts the countdown for everyone in the roster, after filling the grid with bots. Returns false outside the lobby.</summary>
        public bool Start(int laps)
        {
            if (Phase != RacePhase.Lobby || laps < 1 || laps > byte.MaxValue)
            {
                return false;
            }

            _session.AddBotsForEvent();
            var ids = new int[_session.Roster.Count];
            _names.Clear();
            for (int i = 0; i < ids.Length; i++)
            {
                var player = _session.Roster.Players[i];
                ids[i] = player.PlayerId;
                _names[player.PlayerId] = player.Name;
            }

            Tracker = new RaceTracker(_checkpointCount, laps, ids);
            LapCount = laps;
            StartAtLocalMs = _clockMs() + StartLeadMs + CountdownMs;
            Standings = null;
            Results = null;
            _firstFinishMs = -1;
            _nextStandingsMs = StartAtLocalMs;
            _session.EventInProgress = true;
            Phase = RacePhase.Countdown;
            _session.Broadcast(new RaceCountdownMessage { StartAtHostMs = StartAtLocalMs, LapCount = (byte)laps }, reliable: true);
            return true;
        }

        public void Tick()
        {
            long now = _clockMs();
            if (Phase == RacePhase.Countdown && now >= StartAtLocalMs)
            {
                Phase = RacePhase.Racing;
            }

            if (Phase != RacePhase.Racing)
            {
                return;
            }

            if (now >= _nextStandingsMs)
            {
                _nextStandingsMs = now + StandingsIntervalMs;
                Standings = BuildStandings();
                _session.Broadcast(Standings, reliable: false);
            }

            if (Tracker.AllFinished || (_firstFinishMs >= 0 && now >= _firstFinishMs + PostFinishMs))
            {
                End(now);
            }
        }

        public void ReportOwnCheckpoint(int index) => Report(_session.HostPlayerId, index, _clockMs() - StartAtLocalMs);

        /// <summary>A bot's car (which the host drives) crossed checkpoint <paramref name="index"/>.</summary>
        public void ReportBotCheckpoint(byte botId, int index)
        {
            if (_session.IsBot(botId))
            {
                Report(botId, index, _clockMs() - StartAtLocalMs);
            }
        }

        /// <summary>From the results screen: everyone back to the lobby, Ready cleared, joining reopened.</summary>
        public bool ReturnToLobby()
        {
            if (Phase != RacePhase.Results)
            {
                return false;
            }

            Phase = RacePhase.Lobby;
            _session.EventInProgress = false;
            _session.Broadcast(new ReturnToLobbyMessage(), reliable: true);
            _session.RemoveBots();
            _session.ResetReady();
            return true;
        }

        /// <summary>
        /// A guest who leaves mid-event is retired (design doc §12; a bot takes over once bots
        /// exist), so the race can end as soon as everyone still racing has finished.
        /// </summary>
        private void OnPlayerLeft(byte playerId, string name)
        {
            if (Phase == RacePhase.Countdown || Phase == RacePhase.Racing)
            {
                Tracker.Retire(playerId);
            }
        }

        private void OnCheckpoint(CheckpointReachedMessage message)
        {
            // Trust the guest's race time only within what has actually elapsed on the host.
            long elapsed = _clockMs() - StartAtLocalMs;
            Report(message.PlayerId, message.CheckpointIndex, Math.Max(0, Math.Min(message.RaceTimeMs, elapsed)));
        }

        private void Report(byte playerId, int index, long raceTimeMs)
        {
            if (Phase != RacePhase.Racing)
            {
                return;
            }

            if (Tracker.ReportCheckpoint(playerId, index, raceTimeMs) != CheckpointResult.Finished)
            {
                return;
            }

            if (_firstFinishMs < 0)
            {
                _firstFinishMs = _clockMs();
            }

            var racer = Tracker.Get(playerId);
            _session.Broadcast(new PlayerFinishedMessage { PlayerId = playerId, FinishPosition = (byte)racer.FinishPosition, RaceTimeMs = racer.FinishTimeMs }, reliable: true);
        }

        private void End(long now)
        {
            Tracker.FinishRemaining(_fractionToNextCheckpoint, now - StartAtLocalMs);
            Standings = BuildStandings();
            Results = new RaceResultsMessage();
            foreach (int id in Tracker.FinishOrder)
            {
                var racer = Tracker.Get(id);
                Results.Results.Add(new RaceResultEntry
                {
                    PlayerId = (byte)id,
                    Name = _names.TryGetValue(id, out string name) ? name : string.Empty,
                    Position = (byte)racer.FinishPosition,
                    RaceTimeMs = racer.FinishTimeMs,
                    DidNotFinish = racer.DidNotFinish,
                });
            }

            Phase = RacePhase.Results;
            _session.Broadcast(Results, reliable: true);
        }

        private StandingsMessage BuildStandings()
        {
            var message = new StandingsMessage();
            foreach (int id in Tracker.ComputeStandings(_fractionToNextCheckpoint))
            {
                var racer = Tracker.Get(id);
                message.Order.Add(new StandingsEntry
                {
                    PlayerId = (byte)id,
                    LapsCompleted = (byte)Math.Min(racer.LapsCompleted, byte.MaxValue),
                    FinishPosition = (byte)racer.FinishPosition,
                });
            }

            return message;
        }
    }

    /// <summary>A guest's view of the race: follows the host's messages and reports its own checkpoints.</summary>
    public sealed class GuestRace : IRaceView
    {
        private readonly GuestSession _session;
        private readonly Func<long> _clockMs;

        public GuestRace(GuestSession session, Func<long> clockMs)
        {
            _session = session;
            _clockMs = clockMs;
            _session.RaceMessageReceived += OnMessage;
        }

        public RacePhase Phase { get; private set; } = RacePhase.Lobby;
        public int LapCount { get; private set; }
        public long StartAtLocalMs { get; private set; }
        public StandingsMessage Standings { get; private set; }
        public RaceResultsMessage Results { get; private set; }

        public void Tick()
        {
            if (Phase == RacePhase.Countdown && _clockMs() >= StartAtLocalMs)
            {
                Phase = RacePhase.Racing;
            }
        }

        public void ReportOwnCheckpoint(int index)
        {
            if (Phase != RacePhase.Racing)
            {
                return;
            }

            _session.SendToHost(new CheckpointReachedMessage
            {
                PlayerId = _session.PlayerId,
                CheckpointIndex = (ushort)index,
                RaceTimeMs = _clockMs() - StartAtLocalMs,
            }, reliable: true);
        }

        private void OnMessage(IGameMessage message)
        {
            switch (message)
            {
                case RaceCountdownMessage countdown:
                    Phase = RacePhase.Countdown;
                    LapCount = countdown.LapCount;
                    StartAtLocalMs = _session.Clock.ToLocalTime(countdown.StartAtHostMs);
                    Standings = null;
                    Results = null;
                    Tick();
                    break;
                case StandingsMessage standings when Phase == RacePhase.Racing || Phase == RacePhase.Countdown:
                    Standings = standings;
                    break;
                case RaceResultsMessage results:
                    Results = results;
                    Phase = RacePhase.Results;
                    break;
                case ReturnToLobbyMessage _:
                    Phase = RacePhase.Lobby;
                    break;
            }
        }
    }
}
