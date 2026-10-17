using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Physics;
using CarRacer.Core.Race;
using CarRacer.Game.DriveTest;
using CarRacer.Game.Driving;
using CarRacer.Game.Items;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// Puts everyone in a <see cref="NetworkSession"/> on the track (design doc §2) and runs this
    /// phone's part of a race (§7.2): spawns its car on its grid slot once it has a player id,
    /// sends its state at 30 Hz, shows every other player as a <see cref="RemoteCar"/> that
    /// follows the roster, bumps its car off the others (§2.2), holds the car on the grid through
    /// the countdown, and reports the checkpoints it crosses. In a race it also picks up item boxes
    /// and uses the held item (design doc §8), applying Boost and Shield to its own car at once.
    /// </summary>
    public sealed class NetworkedCars : MonoBehaviour
    {
        public const float SendIntervalSeconds = 1f / 30f;

        private readonly Dictionary<byte, RemoteCar> _remotes = new Dictionary<byte, RemoteCar>();
        private readonly HashSet<byte> _inRoster = new HashSet<byte>();
        private readonly List<byte> _gone = new List<byte>();
        private readonly BumpMonitor _bumpMonitor = new BumpMonitor();
        private readonly List<OtherCar> _others = new List<OtherCar>();
        private readonly List<Bump> _bumps = new List<Bump>();
        private readonly Shield _shield = new Shield();
        private Transform _shieldRing;
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

        /// <summary>Every other player's car, for the mini-map.</summary>
        public IEnumerable<RemoteCar> RemoteCars => _remotes.Values;

        public void Initialise(DriveTestArena arena, NetworkSession session)
        {
            _arena = arena;
            _session = session;
            _session.CarStateReceived += OnCarState;
            _session.HitReceived += OnHit;
            _detector = new CheckpointDetector(arena.Track.CheckpointGates());
            _session.CreateRace(arena.Track.CheckpointCount, FractionToNextCheckpoint, arena.Track.ItemBoxPositions());
            gameObject.AddComponent<ItemBoxes>().Initialise(_session.Items);
            gameObject.AddComponent<SessionOverlay>().Initialise(session, this);
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.CarStateReceived -= OnCarState;
                _session.HitReceived -= OnHit;
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

            UseItems();
            SendOwnState(now);
        }

        /// <summary>Uses the held item when the player presses Item, and keeps the Item button and shield ring up to date.</summary>
        private void UseItems()
        {
            var car = _arena.Car;
            var input = _arena.PlayerInput;
            bool pressed = input != null && input.ConsumeItemPress();
            _shield.Tick(Time.deltaTime);
            if (pressed && car != null && _phase == RacePhase.Racing)
            {
                var item = _session.Items.UseHeld(car.BodyPosition.ToCore(), car.BodyVelocity.ToCore(), car.State.Heading);
                if (item == ItemKind.Boost)
                {
                    car.Boost(car.Tuning.ItemBoostSpeed, car.Tuning.ItemBoostDuration);
                }
                else if (item == ItemKind.Shield)
                {
                    _shield.Raise();
                }
            }

            _arena.ShowItemButton = _phase == RacePhase.Racing;
            _arena.ItemButtonLabel = ItemNames.Label(_session.Items.Held);
            if (car != null && _shieldRing == null)
            {
                _shieldRing = ShieldRing.Build(car.transform);
            }

            ShieldRing.Show(_shieldRing, _shield.IsUp);
        }

        private void OnHit(HitMessage hit)
        {
            var car = _arena.Car;
            if (car != null && _bumpMonitor.OnHit(hit, _me, NetworkSession.NowMs, out var impulse))
            {
                car.AddImpulse(impulse.ToUnity());
            }
        }

        private void FixedUpdate()
        {
            var car = _arena.Car;
            if (car == null || _session.Ended)
            {
                return;
            }

            ResolveBumps(car);
            if (_phase != RacePhase.Racing)
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

            _session.Items.ClaimTouchedBox(position.ToCore());
        }

        /// <summary>Pushes this phone's car off any car it touches and tells that car's phone, in case it missed the contact.</summary>
        private void ResolveBumps(CarController car)
        {
            _others.Clear();
            foreach (var pair in _remotes)
            {
                if (pair.Value.HasState)
                {
                    _others.Add(new OtherCar(pair.Key, pair.Value.transform.position.ToCore(), pair.Value.Velocity.ToCore()));
                }
            }

            _bumps.Clear();
            _bumpMonitor.Step(_me, car.BodyPosition.ToCore(), car.BodyVelocity.ToCore(), _others, NetworkSession.NowMs, _bumps);
            foreach (var bump in _bumps)
            {
                car.AddImpulse(bump.SelfImpulse.ToUnity());
                _session.SendHit(bump.Fallback);
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
                _shield.Clear();
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

            if (_shield.IsUp)
            {
                flags |= CarStateFlags.Shielded;
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
