using CarRacer.Core.Net;
using CarRacer.Core.Session;
using Xunit;

namespace CarRacer.Core.Tests
{
    /// <summary>A guest that stops hearing from the host (design doc §12): "Reconnecting…", then giving up.</summary>
    public class HostSilenceTests
    {
        private sealed class NullLink : ISessionTransport
        {
            public void Send(ulong connection, IGameMessage message, bool reliable)
            {
            }

            public void Disconnect(ulong connection)
            {
            }
        }

        private long _now = 10_000;
        private readonly GuestSession _guest;

        public HostSilenceTests()
        {
            _guest = new GuestSession(new NullLink(), "Guest", 14, () => _now);
            _guest.OnConnected();
            _guest.OnMessage(new JoinReplyMessage { Result = JoinResult.Accepted, PlayerId = 1 });
        }

        [Fact]
        public void AQuietHostShowsReconnectingThenIsGivenUpOn()
        {
            _now += GuestSession.ReconnectingAfterMs - 1;
            Assert.False(_guest.HostQuiet);
            _now += 1;
            Assert.True(_guest.HostQuiet);
            Assert.False(_guest.HostLost);

            _now += GuestSession.GiveUpAfterMs - GuestSession.ReconnectingAfterMs;
            Assert.True(_guest.HostLost);
        }

        [Fact]
        public void AnyMessageFromTheHostCountsAsHearingFromIt()
        {
            _now += GuestSession.ReconnectingAfterMs * 2;
            Assert.True(_guest.HostQuiet);
            _guest.OnMessage(new ClockPongMessage { GuestSentMs = _now - 10, HostMs = _now });
            Assert.False(_guest.HostQuiet);
        }

        [Fact]
        public void TimeInTheBackgroundDoesNotCount()
        {
            _now += GuestSession.GiveUpAfterMs * 3;
            _guest.ForgiveSilence();
            Assert.False(_guest.HostQuiet);
            Assert.False(_guest.HostLost);
        }

        [Fact]
        public void BeforeJoiningThereIsNothingToLose()
        {
            var connecting = new GuestSession(new NullLink(), "Guest", 14, () => _now);
            connecting.OnConnected();
            _now += GuestSession.GiveUpAfterMs * 2;
            Assert.False(connecting.HostQuiet);
            Assert.False(connecting.HostLost);
        }
    }
}
