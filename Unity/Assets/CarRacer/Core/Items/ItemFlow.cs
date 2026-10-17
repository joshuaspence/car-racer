using System;
using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Session;

namespace CarRacer.Core.Items
{
    /// <summary>What this phone's game needs from the item rules, the same on the host and on guests (design doc §8).</summary>
    public interface IItemView
    {
        /// <summary>Which boxes are up, in this phone's clock.</summary>
        ItemBoxField Boxes { get; }

        /// <summary>The item this phone's player holds.</summary>
        ItemKind Held { get; }

        /// <summary>Oil slicks on the track, in this phone's clock.</summary>
        HazardField Hazards { get; }

        /// <summary>
        /// Someone used an item. This phone's own use is raised at once, so its effects feel instant;
        /// other players' arrive through the host.
        /// </summary>
        event Action<ItemUsedMessage> ItemUsed;

        /// <summary>Call every frame, after the race's own Tick. A new race puts every box back and empties every slot.</summary>
        void Tick();

        /// <summary>Call every physics step with this phone's car position: claims a box the car is touching, if the slot is empty.</summary>
        bool ClaimTouchedBox(Vec3 carPosition);

        /// <summary>
        /// Uses the held item, returning which it was (<see cref="ItemKind.None"/> if nothing was held
        /// or the race isn't on). The caller applies Boost and Shield to its own car at once.
        /// </summary>
        ItemKind UseHeld(Vec3 position, Vec3 velocity, float headingRadians);
    }

    /// <summary>
    /// The host's item referee (design doc §8): grants box claims first come first served, rolls
    /// each item with catch-up odds from the latest standings, keeps every player's slot, relays
    /// item uses to everyone once it has checked the player held that item, and decides every item
    /// hit: a Shockwave's victims at once, an oil slick's when a car drives onto it.
    /// </summary>
    public sealed class HostItems : IItemView
    {
        private readonly HostSession _session;
        private readonly IRaceView _race;
        private readonly Func<long> _clockMs;
        private readonly Func<double> _random01;
        private readonly CarPositionLookup _carPositions;
        private RacePhase _lastPhase = RacePhase.Lobby;
        private ushort _lastEffectId;

        /// <param name="random01">Uniform random numbers in [0, 1) for item rolls.</param>
        /// <param name="carPositions">Where the host sees each player's car, for deciding hits.</param>
        public HostItems(HostSession session, IRaceView race, IReadOnlyList<Vec3> boxPositions, Func<long> clockMs, Func<double> random01, CarPositionLookup carPositions)
        {
            _session = session;
            _race = race;
            _clockMs = clockMs;
            _random01 = random01;
            _carPositions = carPositions;
            Boxes = new ItemBoxField(boxPositions);
            _session.ItemMessageReceived += OnMessage;
            _session.PlayerLeft += (playerId, name) => Slots.Remove(playerId);
        }

        public ItemBoxField Boxes { get; }
        public ItemSlots Slots { get; } = new ItemSlots();
        public ItemKind Held => Slots.Held(_session.HostPlayerId);
        public HazardField Hazards { get; } = new HazardField();

        public event Action<ItemUsedMessage> ItemUsed;

        public void Tick()
        {
            var phase = _race.Phase;
            if (phase == RacePhase.Countdown && _lastPhase != RacePhase.Countdown)
            {
                Boxes.Reset();
                Slots.Clear();
                Hazards.Clear();
            }

            _lastPhase = phase;
            long now = _clockMs();
            Hazards.Expire(now);
            if (phase == RacePhase.Racing)
            {
                CatchCarsOnHazards(now);
            }
        }

        public bool ClaimTouchedBox(Vec3 carPosition)
        {
            int box = Boxes.FindTouched(carPosition, _clockMs());
            return box >= 0 && Grant(_session.HostPlayerId, box);
        }

        public ItemKind UseHeld(Vec3 position, Vec3 velocity, float headingRadians)
        {
            var used = new ItemUsedMessage
            {
                PlayerId = _session.HostPlayerId,
                Item = Held,
                Position = position,
                Velocity = velocity,
                HeadingRadians = headingRadians,
            };

            return Accept(used) ? used.Item : ItemKind.None;
        }

        private void OnMessage(IGameMessage message)
        {
            switch (message)
            {
                case ItemBoxClaimMessage claim:
                    Grant(claim.PlayerId, claim.BoxIndex);
                    break;
                case ItemUsedMessage used:
                    Accept(used);
                    break;
            }
        }

        private bool Grant(byte playerId, int box)
        {
            long now = _clockMs();
            if (_race.Phase != RacePhase.Racing || !Slots.CanPickUp(playerId) || !Boxes.TryTake(box, now, out long respawnAt))
            {
                return false;
            }

            RacePosition(playerId, out int position, out int racers);
            var item = ItemRoll.Roll(position, racers, _random01());
            Slots.Give(playerId, item);
            _session.Broadcast(new ItemBoxTakenMessage { BoxIndex = (ushort)box, PlayerId = playerId, Item = item, RespawnAtHostMs = respawnAt }, reliable: true);
            return true;
        }

        private bool Accept(ItemUsedMessage used)
        {
            if (_race.Phase != RacePhase.Racing || !Slots.TryUse(used.PlayerId, used.Item))
            {
                return false;
            }

            used.UsedAtHostMs = _clockMs();
            used.EffectId = ++_lastEffectId;
            _session.Broadcast(used, reliable: true);
            ItemUsed?.Invoke(used);

            if (used.Item == ItemKind.Shockwave)
            {
                HitEveryoneNear(used);
            }
            else if (used.Item == ItemKind.OilSlick)
            {
                Hazards.AddOil(used, used.UsedAtHostMs);
            }

            return true;
        }

        private void HitEveryoneNear(ItemUsedMessage used)
        {
            foreach (var player in _session.Roster.Players)
            {
                if (player.PlayerId != used.PlayerId && _carPositions(player.PlayerId, out var position)
                    && (position - used.Position).Flattened.LengthSquared <= ItemEffects.ShockwaveRadius * ItemEffects.ShockwaveRadius)
                {
                    Hit(used.PlayerId, player.PlayerId, ItemEffects.ShockwaveImpulse(used.Position, position));
                }
            }
        }

        /// <summary>Each armed oil slick catches the first car on it (its dropper included, once armed) and is used up.</summary>
        private void CatchCarsOnHazards(long now)
        {
            if (Hazards.Active.Count == 0)
            {
                return;
            }

            foreach (var player in _session.Roster.Players)
            {
                if (_carPositions(player.PlayerId, out var position) && Hazards.TryFindTouched(position, now, out var hazard))
                {
                    Hazards.Remove(hazard.EffectId);
                    _session.Broadcast(new HazardGoneMessage { EffectId = hazard.EffectId }, reliable: true);
                    Hit(hazard.OwnerId, player.PlayerId, ItemEffects.OilImpulse);
                }
            }
        }

        private void Hit(byte attackerId, byte targetId, Vec3 impulse)
        {
            _session.SendHit(new HitMessage { AttackerId = attackerId, TargetId = targetId, SentAtMs = _clockMs(), Impulse = impulse, IsItemHit = true });
        }

        /// <summary>The player's place in the latest standings; mid-pack before the first standings of a race.</summary>
        private void RacePosition(byte playerId, out int position, out int racers)
        {
            var standings = _race.Standings;
            if (standings != null)
            {
                for (int i = 0; i < standings.Order.Count; i++)
                {
                    if (standings.Order[i].PlayerId == playerId)
                    {
                        position = i + 1;
                        racers = standings.Order.Count;
                        return;
                    }
                }
            }

            racers = Math.Max(1, _session.Roster.Count);
            position = (racers + 1) / 2;
        }
    }

    /// <summary>
    /// A guest's side of the items: claims boxes its car drives through (hiding them at once), learns
    /// its item from the host, tells the host when it uses it, and mirrors the oil slicks on the track.
    /// </summary>
    public sealed class GuestItems : IItemView
    {
        /// <summary>A claim the host never answered (a lost message) stops blocking new claims after this long.</summary>
        public const long ClaimTimeoutMs = 1000;

        private readonly GuestSession _session;
        private readonly IRaceView _race;
        private readonly Func<long> _clockMs;
        private RacePhase _lastPhase = RacePhase.Lobby;
        private int _pendingBox = -1;
        private long _pendingSinceMs;

        public GuestItems(GuestSession session, IRaceView race, IReadOnlyList<Vec3> boxPositions, Func<long> clockMs)
        {
            _session = session;
            _race = race;
            _clockMs = clockMs;
            Boxes = new ItemBoxField(boxPositions);
            _session.ItemMessageReceived += OnMessage;
        }

        public ItemBoxField Boxes { get; }
        public ItemKind Held { get; private set; }
        public HazardField Hazards { get; } = new HazardField();

        public event Action<ItemUsedMessage> ItemUsed;

        public void Tick()
        {
            var phase = _race.Phase;
            if (phase == RacePhase.Countdown && _lastPhase != RacePhase.Countdown)
            {
                Boxes.Reset();
                Hazards.Clear();
                Held = ItemKind.None;
                _pendingBox = -1;
            }

            _lastPhase = phase;
            Hazards.Expire(_clockMs());
        }

        public bool ClaimTouchedBox(Vec3 carPosition)
        {
            long now = _clockMs();
            if (_pendingBox >= 0 && now - _pendingSinceMs >= ClaimTimeoutMs)
            {
                _pendingBox = -1;
            }

            if (_race.Phase != RacePhase.Racing || Held != ItemKind.None || _pendingBox >= 0)
            {
                return false;
            }

            int box = Boxes.FindTouched(carPosition, now);
            if (box < 0)
            {
                return false;
            }

            // Hide it at once; the host's answer corrects the respawn time.
            Boxes.MarkTaken(box, now + ItemBoxField.RespawnMs);
            _pendingBox = box;
            _pendingSinceMs = now;
            _session.SendToHost(new ItemBoxClaimMessage { PlayerId = _session.PlayerId, BoxIndex = (ushort)box }, reliable: true);
            return true;
        }

        public ItemKind UseHeld(Vec3 position, Vec3 velocity, float headingRadians)
        {
            var item = Held;
            if (_race.Phase != RacePhase.Racing || item == ItemKind.None)
            {
                return ItemKind.None;
            }

            Held = ItemKind.None;
            var used = new ItemUsedMessage
            {
                PlayerId = _session.PlayerId,
                Item = item,
                Position = position,
                Velocity = velocity,
                HeadingRadians = headingRadians,
            };
            _session.SendToHost(used, reliable: true);
            ItemUsed?.Invoke(used);
            return item;
        }

        private void OnMessage(IGameMessage message)
        {
            switch (message)
            {
                case ItemBoxTakenMessage taken:
                    Boxes.MarkTaken(taken.BoxIndex, _session.Clock.ToLocalTime(taken.RespawnAtHostMs));
                    if (taken.PlayerId == _session.PlayerId)
                    {
                        Held = taken.Item;
                    }

                    if (taken.BoxIndex == _pendingBox)
                    {
                        _pendingBox = -1;
                    }

                    break;
                case ItemUsedMessage used:
                    // The host's relay carries the effect id, so hazards come from it even for this phone's own use.
                    if (used.Item == ItemKind.OilSlick)
                    {
                        Hazards.AddOil(used, _session.Clock.ToLocalTime(used.UsedAtHostMs));
                    }

                    if (used.PlayerId != _session.PlayerId)
                    {
                        ItemUsed?.Invoke(used);
                    }

                    break;
                case HazardGoneMessage gone:
                    Hazards.Remove(gone.EffectId);
                    break;
            }
        }
    }
}
