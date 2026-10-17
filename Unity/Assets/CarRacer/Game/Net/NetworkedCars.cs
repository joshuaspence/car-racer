using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Game.DriveTest;
using CarRacer.Game.Driving;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// Puts everyone in a <see cref="NetworkSession"/> on the track (design doc §2) and runs this
    /// phone's part of a race (§7.2): spawns its car on its grid slot once it has a player id,
    /// sends its state at 30 Hz, shows every other player as a <see cref="RemoteCar"/> that
    /// follows the roster, holds the car on the grid through the countdown, and reports the
    /// checkpoints it crosses.
    /// </summary>
    public sealed class NetworkedCars : MonoBehaviour
    {
        public const float SendIntervalSeconds = 1f / 30f;

        private readonly Dictionary<byte, RemoteCar> _remotes = new Dictionary<byte, RemoteCar>();
        private readonly HashSet<byte> _inRoster = new HashSet<byte>();
        private readonly List<byte> _gone = new List<byte>();
        private DriveTestArena _arena;
        private NetworkSession _session;
        private CheckpointDetector _detector;
        private RacePhase _phase = RacePhase.Lobby;
        private Vector3 _lastPosition;
        private bool _spawned;
        private byte _me;
        private uint _sequence;
        private float _sendTimer;

        /// <summary>This phone's own lap count and next checkpoint, for the HUD.</summary>
        public CheckpointDetector Detector => _detector;

        public void Initialise(DriveTestArena arena, NetworkSession session)
        {
            _arena = arena;
            _session = session;
            _session.CarStateReceived += OnCarState;
            _detector = new CheckpointDetector(arena.Track.CheckpointGates());
            _session.CreateRace(arena.Track.CheckpointCount, FractionToNextCheckpoint);
            gameObject.AddComponent<SessionOverlay>().Initialise(session, this);
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.CarStateReceived -= OnCarState;
            }
        }

        private void OnCarState(CarStateMessage state)
        {
            if (_remotes.TryGetValue(state.PlayerId, out var car))
            {
                car.OnState(state, NetworkSession.NowMs, _session.LatencyMsFor(state.PlayerId));
            }
        }

        private void Update()
        {
            if (_session.Ended || !_session.TryGetLocalPlayerId(out _me))
            {
                return;
            }

            SyncRoster();
            FollowRacePhase();

            long now = NetworkSession.NowMs;
            foreach (var pair in _remotes)
            {
                pair.Value.Tick(now, _session.LatencyMsFor(pair.Key), Time.deltaTime);
            }

            SendOwnState(now);
        }

        private void FixedUpdate()
        {
            var car = _arena.Car;
            if (car == null || _phase != RacePhase.Racing)
            {
                return;
            }

            Vector3 position = car.BodyPosition;
            int crossed = _detector.Update(_lastPosition.ToCore(), position.ToCore());
            _lastPosition = position;
            if (crossed >= 0)
            {
                _session.Race.ReportOwnCheckpoint(crossed);
            }
        }

        private void FollowRacePhase()
        {
            var phase = _session.Race.Phase;
            if (phase == _phase || _arena.Car == null)
            {
                return;
            }

            // Line up on the grid for the countdown (or straight away if GO came before the countdown was seen).
            if (phase == RacePhase.Countdown || (phase == RacePhase.Racing && _phase != RacePhase.Countdown))
            {
                _arena.PlaceLocalCarOnGrid(_me);
                _detector.Reset();
                _lastPosition = _arena.Car.BodyPosition;
            }

            _arena.Car.InputLocked = phase == RacePhase.Countdown;
            _phase = phase;
        }

        private void SendOwnState(long now)
        {
            var car = _arena.Car;
            if (car == null)
            {
                return;
            }

            _sendTimer += Time.deltaTime;
            if (_sendTimer < SendIntervalSeconds)
            {
                return;
            }

            _sendTimer = Mathf.Min(_sendTimer - SendIntervalSeconds, SendIntervalSeconds);
            var state = car.State;
            var flags = CarStateFlags.None;
            if (state.Drifting)
            {
                flags |= CarStateFlags.Drifting;
            }

            if (state.BoostSecondsLeft > 0f)
            {
                flags |= CarStateFlags.Boosting;
            }

            _session.SendCarState(new CarStateMessage
            {
                PlayerId = _me,
                Sequence = ++_sequence,
                SentAtMs = now,
                Position = car.BodyPosition.ToCore(),
                Velocity = car.BodyVelocity.ToCore(),
                HeadingRadians = state.Heading,
                Flags = flags,
            });
        }

        /// <summary>Host only, for standings: how far a player's car is towards its next checkpoint.</summary>
        private float FractionToNextCheckpoint(int playerId)
        {
            Vector3 position;
            if (playerId == _me && _arena.Car != null)
            {
                position = _arena.Car.BodyPosition;
            }
            else if (_remotes.TryGetValue((byte)playerId, out var remote) && remote.HasState)
            {
                position = remote.transform.position;
            }
            else
            {
                return 0f;
            }

            var tracker = (_session.Race as HostRace)?.Tracker;
            if (tracker == null)
            {
                return 0f;
            }

            float distance = _arena.Track.Spline.FindNearestDistance(position.ToCore());
            return _arena.Track.FractionToNextCheckpoint(distance, tracker.Get(playerId).NextCheckpoint);
        }

        /// <summary>Spawns this phone's car once its roster entry (and colour) is known, and adds or removes remote cars to match the roster.</summary>
        private void SyncRoster()
        {
            _inRoster.Clear();
            foreach (var player in _session.Players)
            {
                _inRoster.Add(player.PlayerId);
                if (player.PlayerId == _me)
                {
                    if (!_spawned)
                    {
                        _arena.SpawnLocalCar(player.PlayerId, CarColours.ForIndex(player.ColourIndex));
                        _spawned = true;
                    }
                }
                else if (!_remotes.ContainsKey(player.PlayerId))
                {
                    _remotes[player.PlayerId] = CarFactory.BuildRemote($"Remote car {player.PlayerId}", player.PlayerId, CarColours.ForIndex(player.ColourIndex), _arena.transform);
                }
            }

            _gone.Clear();
            foreach (byte id in _remotes.Keys)
            {
                if (!_inRoster.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (byte id in _gone)
            {
                Destroy(_remotes[id].gameObject);
                _remotes.Remove(id);
            }
        }
    }
}
