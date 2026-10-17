using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Game.DriveTest;
using CarRacer.Game.Driving;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// Puts everyone in a <see cref="NetworkSession"/> on the track (design doc §2): spawns this
    /// phone's car on its grid slot once it has a player id, sends its state at 30 Hz, and shows
    /// every other player as a <see cref="RemoteCar"/> that follows the roster.
    /// </summary>
    public sealed class NetworkedCars : MonoBehaviour
    {
        public const float SendIntervalSeconds = 1f / 30f;

        private readonly Dictionary<byte, RemoteCar> _remotes = new Dictionary<byte, RemoteCar>();
        private readonly HashSet<byte> _inRoster = new HashSet<byte>();
        private readonly List<byte> _gone = new List<byte>();
        private DriveTestArena _arena;
        private NetworkSession _session;
        private bool _spawned;
        private uint _sequence;
        private float _sendTimer;

        public void Initialise(DriveTestArena arena, NetworkSession session)
        {
            _arena = arena;
            _session = session;
            _session.CarStateReceived += OnCarState;
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
            if (_session.Ended || !_session.TryGetLocalPlayerId(out byte me))
            {
                return;
            }

            SyncRoster(me);

            long now = NetworkSession.NowMs;
            foreach (var pair in _remotes)
            {
                pair.Value.Tick(now, _session.LatencyMsFor(pair.Key), Time.deltaTime);
            }

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
                PlayerId = me,
                Sequence = ++_sequence,
                SentAtMs = now,
                Position = car.BodyPosition.ToCore(),
                Velocity = car.BodyVelocity.ToCore(),
                HeadingRadians = state.Heading,
                Flags = flags,
            });
        }

        /// <summary>Spawns this phone's car once its roster entry (and colour) is known, and adds or removes remote cars to match the roster.</summary>
        private void SyncRoster(byte me)
        {
            _inRoster.Clear();
            foreach (var player in _session.Players)
            {
                _inRoster.Add(player.PlayerId);
                if (player.PlayerId == me)
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
