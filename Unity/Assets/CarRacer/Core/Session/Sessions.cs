using System;
using System.Collections.Generic;
using CarRacer.Core.Net;

namespace CarRacer.Core.Session
{
    /// <summary>
    /// What a session needs from the network layer (NGO in the game, a fake in tests). Connections
    /// are identified by the transport's client id; the host is always <see cref="GuestSession.HostConnection"/>.
    /// </summary>
    public interface ISessionTransport
    {
        void Send(ulong connection, IGameMessage message, bool reliable);

        void Disconnect(ulong connection);
    }

    /// <summary>
    /// The host's side of the session protocol (design doc §2.1): admits or refuses guests,
    /// keeps the roster, answers clock pings, and relays car states between guests (NGO guests
    /// can only reach the host). Independent of NGO so it can be unit tested.
    /// </summary>
    public sealed class HostSession
    {
        private readonly ISessionTransport _transport;
        private readonly Func<long> _clockMs;
        private readonly ushort _protocolVersion;
        private readonly Dictionary<ulong, byte> _playerByConnection = new Dictionary<ulong, byte>();

        public HostSession(ISessionTransport transport, string hostName, ushort protocolVersion, Func<long> clockMs)
        {
            _transport = transport;
            _protocolVersion = protocolVersion;
            _clockMs = clockMs;
            HostPlayerId = Roster.Add(hostName, isHost: true).PlayerId;
        }

        public LobbyRoster Roster { get; } = new LobbyRoster();
        public byte HostPlayerId { get; }

        /// <summary>While true, new guests are refused (no joining mid-event, design doc §12).</summary>
        public bool EventInProgress { get; set; }

        /// <summary>Raised after anyone joins, leaves or changes Ready.</summary>
        public event Action RosterChanged;

        /// <summary>A guest's car state arrived (already relayed to the other guests).</summary>
        public event Action<CarStateMessage> CarStateReceived;

        /// <summary>A joined guest reported a checkpoint for its own car (see <c>HostRace</c>).</summary>
        public event Action<CheckpointReachedMessage> CheckpointReceived;

        /// <summary>A guest's car bumped the host's car (fallback hit, see <c>BumpMonitor</c>).</summary>
        public event Action<HitMessage> HitReceived;

        public IEnumerable<ulong> GuestConnections => _playerByConnection.Keys;

        public bool TryGetPlayerId(ulong connection, out byte playerId) => _playerByConnection.TryGetValue(connection, out playerId);

        public void OnDisconnected(ulong connection)
        {
            if (_playerByConnection.TryGetValue(connection, out byte playerId))
            {
                _playerByConnection.Remove(connection);
                Roster.Remove(playerId);
                BroadcastRoster();
            }
        }

        public void OnMessage(ulong connection, IGameMessage message)
        {
            bool joined = _playerByConnection.TryGetValue(connection, out byte playerId);
            switch (message)
            {
                case JoinRequestMessage join when !joined:
                    HandleJoin(connection, join);
                    break;
                case ClockPingMessage ping:
                    _transport.Send(connection, new ClockPongMessage { GuestSentMs = ping.GuestSentMs, HostMs = _clockMs() }, reliable: false);
                    break;
                case SetReadyMessage ready when joined:
                    if (Roster.SetReady(playerId, ready.Ready))
                    {
                        BroadcastRoster();
                    }

                    break;
                case CarStateMessage state when joined && state.PlayerId == playerId:
                    // A guest may only speak for its own car.
                    foreach (ulong other in _playerByConnection.Keys)
                    {
                        if (other != connection)
                        {
                            _transport.Send(other, state, reliable: false);
                        }
                    }

                    CarStateReceived?.Invoke(state);
                    break;
                case CheckpointReachedMessage checkpoint when joined && checkpoint.PlayerId == playerId:
                    CheckpointReceived?.Invoke(checkpoint);
                    break;
                case HitMessage hit when joined && hit.AttackerId == playerId && !hit.IsItemHit:
                    // Item hits come only from the host; a guest may only report bumps by its own car.
                    SendHit(hit);
                    break;
            }
        }

        /// <summary>Sends the host's own car (or a bot's) to every guest.</summary>
        public void BroadcastCarState(CarStateMessage state) => Broadcast(state, reliable: false);

        /// <summary>Clears every guest's Ready (the lobby reopening after an event) and tells everyone.</summary>
        public void ResetReady()
        {
            Roster.ResetReady();
            BroadcastRoster();
        }

        public void SetHostName(string name)
        {
            Roster.Find(HostPlayerId).Name = LobbyRoster.CleanName(name);
            BroadcastRoster();
        }

        private void HandleJoin(ulong connection, JoinRequestMessage join)
        {
            var result = Roster.CheckJoin(join.ProtocolVersion, _protocolVersion, EventInProgress);
            if (result != JoinResult.Accepted)
            {
                _transport.Send(connection, new JoinReplyMessage { Result = result }, reliable: true);
                _transport.Disconnect(connection);
                return;
            }

            var player = Roster.Add(join.PlayerName);
            _playerByConnection[connection] = player.PlayerId;
            _transport.Send(connection, new JoinReplyMessage { Result = JoinResult.Accepted, PlayerId = player.PlayerId }, reliable: true);
            BroadcastRoster();
        }

        private void BroadcastRoster()
        {
            Broadcast(Roster.ToMessage(), reliable: true);
            RosterChanged?.Invoke();
        }

        /// <summary>Delivers a hit to its target: the host's own car, or the guest that owns the target car.</summary>
        public void SendHit(HitMessage hit)
        {
            if (hit.TargetId == HostPlayerId)
            {
                HitReceived?.Invoke(hit);
                return;
            }

            foreach (var pair in _playerByConnection)
            {
                if (pair.Value == hit.TargetId)
                {
                    _transport.Send(pair.Key, hit, reliable: true);
                    return;
                }
            }
        }

        /// <summary>Sends a message to every joined guest.</summary>
        public void Broadcast(IGameMessage message, bool reliable)
        {
            foreach (ulong connection in _playerByConnection.Keys)
            {
                _transport.Send(connection, message, reliable);
            }
        }
    }

    public enum GuestState
    {
        Connecting,
        Joined,
        Refused,
    }

    /// <summary>
    /// A guest's side of the session protocol: asks to join, tracks the roster, keeps the clock
    /// offset fresh, and passes on other players' car states.
    /// </summary>
    public sealed class GuestSession
    {
        /// <summary>NGO's server client id.</summary>
        public const ulong HostConnection = 0;

        /// <summary>Clock pings go out quickly at first for a good estimate before a race starts, then once a second.</summary>
        public const long FastPingIntervalMs = 200;
        public const long PingIntervalMs = 1000;
        public const int FastPingCount = 5;

        private readonly ISessionTransport _transport;
        private readonly Func<long> _clockMs;
        private readonly string _playerName;
        private readonly ushort _protocolVersion;
        private long _nextPingMs;
        private int _pingsSent;

        public GuestSession(ISessionTransport transport, string playerName, ushort protocolVersion, Func<long> clockMs)
        {
            _transport = transport;
            _playerName = playerName;
            _protocolVersion = protocolVersion;
            _clockMs = clockMs;
        }

        public GuestState State { get; private set; } = GuestState.Connecting;
        public JoinResult RefusalReason { get; private set; }
        public byte PlayerId { get; private set; }
        public RosterMessage Roster { get; private set; } = new RosterMessage();
        public ClockSync Clock { get; } = new ClockSync();
        public bool Ready { get; private set; }

        public event Action RosterChanged;
        public event Action<CarStateMessage> CarStateReceived;

        /// <summary>Race messages from the host (countdown, standings, finishes, results, back to lobby); see <c>GuestRace</c>.</summary>
        public event Action<IGameMessage> RaceMessageReceived;

        /// <summary>Another car bumped this guest's car, or the host scored an item hit on it.</summary>
        public event Action<HitMessage> HitReceived;

        /// <summary>Call once the transport has connected to the host.</summary>
        public void OnConnected()
        {
            _transport.Send(HostConnection, new JoinRequestMessage { ProtocolVersion = _protocolVersion, PlayerName = LobbyRoster.CleanName(_playerName) }, reliable: true);
        }

        public void OnMessage(IGameMessage message)
        {
            switch (message)
            {
                case JoinReplyMessage reply when State == GuestState.Connecting:
                    if (reply.Result == JoinResult.Accepted)
                    {
                        State = GuestState.Joined;
                        PlayerId = reply.PlayerId;
                        _nextPingMs = _clockMs();
                    }
                    else
                    {
                        State = GuestState.Refused;
                        RefusalReason = reply.Result;
                    }

                    break;
                case RosterMessage roster:
                    Roster = roster;
                    RosterChanged?.Invoke();
                    break;
                case ClockPongMessage pong:
                    Clock.AddSample(pong.GuestSentMs, pong.HostMs, _clockMs());
                    break;
                case CarStateMessage state when state.PlayerId != PlayerId:
                    CarStateReceived?.Invoke(state);
                    break;
                case HitMessage hit when State == GuestState.Joined && hit.TargetId == PlayerId:
                    HitReceived?.Invoke(hit);
                    break;
                case RaceCountdownMessage _:
                case StandingsMessage _:
                case PlayerFinishedMessage _:
                case RaceResultsMessage _:
                case ReturnToLobbyMessage _:
                    if (State == GuestState.Joined)
                    {
                        RaceMessageReceived?.Invoke(message);
                    }

                    break;
            }
        }

        /// <summary>Call every frame: sends clock pings when due.</summary>
        public void Tick()
        {
            long now = _clockMs();
            if (State != GuestState.Joined || now < _nextPingMs)
            {
                return;
            }

            _transport.Send(HostConnection, new ClockPingMessage { GuestSentMs = now }, reliable: false);
            _pingsSent++;
            _nextPingMs = now + (_pingsSent < FastPingCount ? FastPingIntervalMs : PingIntervalMs);
        }

        public void SetReady(bool ready)
        {
            Ready = ready;
            if (State == GuestState.Joined)
            {
                _transport.Send(HostConnection, new SetReadyMessage { Ready = ready }, reliable: true);
            }
        }

        public void SendCarState(CarStateMessage state) => SendToHost(state, reliable: false);

        public void SendToHost(IGameMessage message, bool reliable)
        {
            if (State == GuestState.Joined)
            {
                _transport.Send(HostConnection, message, reliable);
            }
        }
    }
}
