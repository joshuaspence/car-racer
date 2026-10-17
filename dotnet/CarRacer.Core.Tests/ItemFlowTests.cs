using System.Collections.Generic;
using System.Linq;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Session;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class ItemMessageTests
    {
        [Fact]
        public void ItemMessagesRoundTrip()
        {
            var claim = Assert.IsType<ItemBoxClaimMessage>(MessageCodec.Decode(MessageCodec.Encode(new ItemBoxClaimMessage { PlayerId = 3, BoxIndex = 513 })));
            Assert.Equal(3, claim.PlayerId);
            Assert.Equal(513, claim.BoxIndex);

            var taken = Assert.IsType<ItemBoxTakenMessage>(MessageCodec.Decode(MessageCodec.Encode(new ItemBoxTakenMessage
            {
                BoxIndex = 7, PlayerId = 2, Item = ItemKind.Bomb, RespawnAtHostMs = 123_456_789_012,
            })));
            Assert.Equal(7, taken.BoxIndex);
            Assert.Equal(2, taken.PlayerId);
            Assert.Equal(ItemKind.Bomb, taken.Item);
            Assert.Equal(123_456_789_012, taken.RespawnAtHostMs);

            var used = Assert.IsType<ItemUsedMessage>(MessageCodec.Decode(MessageCodec.Encode(new ItemUsedMessage
            {
                PlayerId = 1, Item = ItemKind.Missile, Position = new Vec3(1f, 2f, 3f), Velocity = new Vec3(-4f, 0f, 5.5f), HeadingRadians = 1.25f, UsedAtHostMs = 99,
            })));
            Assert.Equal(ItemKind.Missile, used.Item);
            Assert.Equal(new Vec3(1f, 2f, 3f), used.Position);
            Assert.Equal(new Vec3(-4f, 0f, 5.5f), used.Velocity);
            Assert.Equal(1.25f, used.HeadingRadians);
            Assert.Equal(99, used.UsedAtHostMs);
        }
    }

    public class ItemFlowTests
    {
        private const ushort Version = 5;
        private const ulong GuestConnection = 7;
        private const long GuestClockBehindMs = 1_000_000;

        private static readonly List<Vec3> BoxPositions = new List<Vec3> { new Vec3(0f, 0.75f, 20f), new Vec3(3f, 0.75f, 20f) };

        /// <summary>Delivers the host's messages to the guest, unless held back to simulate latency.</summary>
        private sealed class HostLink : ISessionTransport
        {
            public readonly List<IGameMessage> Sent = new List<IGameMessage>();
            public readonly List<IGameMessage> Held = new List<IGameMessage>();
            public GuestSession Guest;
            public bool HoldBack;

            public void Send(ulong connection, IGameMessage message, bool reliable)
            {
                Sent.Add(message);
                if (HoldBack)
                {
                    Held.Add(message);
                }
                else
                {
                    Guest?.OnMessage(message);
                }
            }

            public void Release()
            {
                HoldBack = false;
                foreach (var m in Held)
                {
                    Guest.OnMessage(m);
                }

                Held.Clear();
            }

            public void Disconnect(ulong connection)
            {
            }
        }

        /// <summary>Delivers the guest's messages to the host, unless dropped.</summary>
        private sealed class GuestLink : ISessionTransport
        {
            public readonly List<IGameMessage> Sent = new List<IGameMessage>();
            public HostSession Host;
            public bool Drop;

            public void Send(ulong connection, IGameMessage message, bool reliable)
            {
                Sent.Add(message);
                if (!Drop)
                {
                    Host.OnMessage(GuestConnection, message);
                }
            }

            public void Disconnect(ulong connection)
            {
            }
        }

        private long _now = 10_000;
        private double _nextRandom = 0.0;
        private readonly HostLink _hostLink = new HostLink();
        private readonly GuestLink _guestLink = new GuestLink();
        private readonly HostSession _host;
        private readonly HostRace _hostRace;
        private readonly HostItems _hostItems;
        private readonly GuestSession _guest;
        private readonly GuestRace _guestRace;
        private readonly GuestItems _guestItems;
        private readonly List<ItemUsedMessage> _hostSaw = new List<ItemUsedMessage>();
        private readonly List<ItemUsedMessage> _guestSaw = new List<ItemUsedMessage>();

        public ItemFlowTests()
        {
            _host = new HostSession(_hostLink, "Host", Version, () => _now);
            _hostRace = new HostRace(_host, 4, () => _now, id => 0f);
            _hostItems = new HostItems(_host, _hostRace, BoxPositions, () => _now, () => _nextRandom);
            _hostItems.ItemUsed += _hostSaw.Add;

            _guestLink.Host = _host;
            _guest = new GuestSession(_guestLink, "Guest", Version, () => _now - GuestClockBehindMs);
            _hostLink.Guest = _guest;
            _guest.OnConnected();
            _guest.OnMessage(new ClockPongMessage { GuestSentMs = _now - GuestClockBehindMs - 20, HostMs = _now - 10 }); // 20 ms round trip
            _guestRace = new GuestRace(_guest, () => _now - GuestClockBehindMs);
            _guestItems = new GuestItems(_guest, _guestRace, BoxPositions, () => _now - GuestClockBehindMs);
            _guestItems.ItemUsed += _guestSaw.Add;
        }

        private byte GuestId => _guest.PlayerId;

        private void Tick()
        {
            _hostRace.Tick();
            _guestRace.Tick();
            _hostItems.Tick();
            _guestItems.Tick();
        }

        private void StartRacing()
        {
            Assert.True(_hostRace.Start(3));
            Tick();
            _now += HostRace.StartLeadMs + HostRace.CountdownMs;
            Tick();
            Assert.Equal(RacePhase.Racing, _hostRace.Phase);
            Assert.Equal(RacePhase.Racing, _guestRace.Phase);
        }

        [Fact]
        public void NoClaimsOutsideARace()
        {
            Assert.False(_hostItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.False(_guestItems.ClaimTouchedBox(BoxPositions[1]));
            Assert.Empty(_hostLink.Sent.OfType<ItemBoxTakenMessage>());
        }

        [Fact]
        public void AGuestDrivingThroughABoxGetsAnItemAndEveryoneSeesTheBoxGo()
        {
            StartRacing();
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[1] + new Vec3(0.5f, -0.5f, 0f)));

            var taken = Assert.Single(_hostLink.Sent.OfType<ItemBoxTakenMessage>());
            Assert.Equal(1, taken.BoxIndex);
            Assert.Equal(GuestId, taken.PlayerId);
            Assert.Equal(_now + ItemBoxField.RespawnMs, taken.RespawnAtHostMs);
            Assert.NotEqual(ItemKind.None, taken.Item);
            Assert.Equal(taken.Item, _guestItems.Held);
            Assert.Equal(taken.Item, _hostItems.Slots.Held(GuestId));
            Assert.Equal(ItemKind.None, _hostItems.Held);

            // Down on both phones until it comes back, in each phone's own clock.
            Assert.False(_hostItems.Boxes.IsUp(1, _now));
            Assert.False(_guestItems.Boxes.IsUp(1, _now - GuestClockBehindMs));
            Assert.True(_hostItems.Boxes.IsUp(1, taken.RespawnAtHostMs));
            Assert.True(_guestItems.Boxes.IsUp(1, _guest.Clock.ToLocalTime(taken.RespawnAtHostMs)));
        }

        [Fact]
        public void TheFirstToArriveGetsTheBox()
        {
            StartRacing();
            _hostLink.HoldBack = true; // the guest hasn't heard yet that the host got there first
            Assert.True(_hostItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            _hostLink.Release();

            var taken = Assert.Single(_hostLink.Sent.OfType<ItemBoxTakenMessage>());
            Assert.Equal(_host.HostPlayerId, taken.PlayerId);
            Assert.NotEqual(ItemKind.None, _hostItems.Held);
            Assert.Equal(ItemKind.None, _guestItems.Held);

            // The host's answer about that box ends the guest's wait, so it can claim the next one.
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[1]));
        }

        [Fact]
        public void APlayerHoldingAnItemLeavesBoxesForOthers()
        {
            StartRacing();
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.False(_guestItems.ClaimTouchedBox(BoxPositions[1]));
            Assert.True(_guestItems.Boxes.IsUp(1, _now - GuestClockBehindMs));

            // Even if a claim gets through, the host refuses a player whose slot is full.
            _host.OnMessage(GuestConnection, new ItemBoxClaimMessage { PlayerId = GuestId, BoxIndex = 1 });
            Assert.Single(_hostLink.Sent.OfType<ItemBoxTakenMessage>());
            Assert.True(_hostItems.Boxes.IsUp(1, _now));
        }

        [Fact]
        public void ALostClaimStopsBlockingAfterATimeout()
        {
            StartRacing();
            _guestLink.Drop = true;
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            _guestLink.Drop = false;
            Assert.False(_guestItems.ClaimTouchedBox(BoxPositions[1]));
            _now += GuestItems.ClaimTimeoutMs;
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[1]));
            Assert.NotEqual(ItemKind.None, _guestItems.Held);
        }

        [Fact]
        public void RollsUseTheLatestStandings()
        {
            StartRacing();
            _now += HostRace.StandingsIntervalMs;
            Tick();
            var order = _hostRace.Standings.Order;
            Assert.Equal(2, order.Count);
            int guestPosition = order.FindIndex(e => e.PlayerId == GuestId) + 1;

            // A number that gives last place a Missile and the leader something else.
            double r = 0.0;
            while (ItemRoll.Roll(2, 2, r) != ItemKind.Missile)
            {
                r += 0.01;
            }

            Assert.NotEqual(ItemKind.Missile, ItemRoll.Roll(1, 2, r));
            _nextRandom = r;
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.True(_hostItems.ClaimTouchedBox(BoxPositions[1]));
            Assert.Equal(ItemRoll.Roll(guestPosition, 2, r), _guestItems.Held);
            Assert.Equal(ItemRoll.Roll(3 - guestPosition, 2, r), _hostItems.Held);
            Assert.Contains(ItemKind.Missile, new[] { _guestItems.Held, _hostItems.Held });
        }

        [Fact]
        public void UsingAnItemTellsEveryoneAndEmptiesTheSlot()
        {
            StartRacing();
            _nextRandom = 0.0; // Boost
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.Equal(ItemKind.Boost, _guestItems.Held);

            Assert.Equal(ItemKind.Boost, _guestItems.UseHeld(new Vec3(1f, 0f, 2f), new Vec3(0f, 0f, 20f), 0.5f));
            Assert.Equal(ItemKind.None, _guestItems.Held);
            Assert.Equal(ItemKind.None, _hostItems.Slots.Held(GuestId));

            // The guest saw its own use at once and doesn't see the host's echo again.
            var own = Assert.Single(_guestSaw);
            Assert.Equal(GuestId, own.PlayerId);
            var relayed = Assert.Single(_hostSaw);
            Assert.Equal(ItemKind.Boost, relayed.Item);
            Assert.Equal(new Vec3(1f, 0f, 2f), relayed.Position);
            Assert.Equal(_now, relayed.UsedAtHostMs);
            Assert.Single(_hostLink.Sent.OfType<ItemUsedMessage>());

            Assert.Equal(ItemKind.None, _guestItems.UseHeld(Vec3.Zero, Vec3.Zero, 0f));
        }

        [Fact]
        public void TheHostsOwnUseReachesTheGuest()
        {
            StartRacing();
            Assert.True(_hostItems.ClaimTouchedBox(BoxPositions[0]));
            var item = _hostItems.Held;
            Assert.Equal(item, _hostItems.UseHeld(Vec3.Zero, Vec3.Zero, 0f));
            Assert.Equal(item, Assert.Single(_guestSaw).Item);
            Assert.Equal(item, Assert.Single(_hostSaw).Item);
            Assert.Equal(ItemKind.None, _hostItems.Held);
        }

        [Fact]
        public void TheHostRefusesAnItemThePlayerDoesNotHold()
        {
            StartRacing();
            _host.OnMessage(GuestConnection, new ItemUsedMessage { PlayerId = GuestId, Item = ItemKind.Missile });
            Assert.Empty(_hostSaw);
            Assert.Empty(_hostLink.Sent.OfType<ItemUsedMessage>());
        }

        [Fact]
        public void AGuestCannotSpeakForAnotherPlayer()
        {
            StartRacing();
            _host.OnMessage(GuestConnection, new ItemBoxClaimMessage { PlayerId = _host.HostPlayerId, BoxIndex = 0 });
            Assert.Empty(_hostLink.Sent.OfType<ItemBoxTakenMessage>());
        }

        [Fact]
        public void ANewRaceRestoresBoxesAndEmptiesSlots()
        {
            StartRacing();
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            Assert.True(_hostItems.ClaimTouchedBox(BoxPositions[1]));

            _hostRace.Tracker.Retire(GuestId);
            _hostRace.Tracker.Retire(_host.HostPlayerId);
            Tick();
            Assert.Equal(RacePhase.Results, _hostRace.Phase);
            Assert.True(_hostRace.ReturnToLobby());
            StartRacing();

            Assert.Equal(ItemKind.None, _guestItems.Held);
            Assert.Equal(ItemKind.None, _hostItems.Held);
            Assert.True(_hostItems.Boxes.IsUp(0, _now - 1));
            Assert.True(_guestItems.Boxes.IsUp(1, _now - GuestClockBehindMs - 1));
        }

        [Fact]
        public void APlayerWhoLeavesLosesTheirItem()
        {
            StartRacing();
            Assert.True(_guestItems.ClaimTouchedBox(BoxPositions[0]));
            _host.OnDisconnected(GuestConnection);
            Assert.Equal(ItemKind.None, _hostItems.Slots.Held(GuestId));
        }
    }
}
