using System.Collections;
using CarRacer.Core.Driving;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Session;
using CarRacer.Game.Driving;
using CarRacer.Game.Net;
using CarRacer.Game.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CarRacer.Tests
{
    /// <summary>
    /// Starts a real NGO host from code (one process can't also join it). The session protocol
    /// itself is tested by `dotnet test`; this checks the NGO and Unity Transport setup.
    /// </summary>
    public class NetworkSessionTests
    {
        private const ushort TestPort = 47790;

        private sealed class NoInput : ICarInputSource
        {
            public CarInput Read() => default;
        }

        [UnityTest]
        public IEnumerator HostingPutsTheHostsCarOnTheGrid()
        {
            var screen = TrackScreen.Create(new NoInput(), () => NetworkSession.StartHosting("Test host", TestPort));
            try
            {
                yield return null;
                yield return null;

                var session = screen.Session;
                Assert.IsFalse(session.Ended, session.Status);
                Assert.IsTrue(session.IsHost);
                Assert.IsTrue(session.TryGetLocalPlayerId(out byte me));
                Assert.AreEqual(0, me);
                Assert.AreEqual(1, session.Players.Count);
                Assert.AreEqual("Test host", session.Players[0].Name);
                Assert.IsNotNull(screen.Car, "the host's car should be spawned on its grid slot");
            }
            finally
            {
                Object.Destroy(screen.gameObject);
            }

            // Let NGO shut down before anything else starts a NetworkManager.
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASoloRaceHoldsTheCarOnTheGridUntilGo()
        {
            var input = new FullThrottle();
            var screen = TrackScreen.Create(input, () => NetworkSession.StartHosting("Solo", TestPort + 1));
            try
            {
                yield return null;
                yield return null;
                var race = (HostRace)screen.Session.Race;
                Assert.IsTrue(race.Start(laps: 1));

                yield return new WaitForSeconds(1f);
                Assert.AreEqual(RacePhase.Countdown, race.Phase);
                Assert.IsTrue(screen.Car.InputLocked);
                Assert.That(screen.Car.ForwardSpeed, Is.EqualTo(0f).Within(0.5f), "held on the grid despite full throttle");

                yield return new WaitForSeconds((HostRace.StartLeadMs + HostRace.CountdownMs) / 1000f);
                Assert.AreEqual(RacePhase.Racing, race.Phase);
                Assert.IsFalse(screen.Car.InputLocked);

                // Driving off the grid crosses the start line, checkpoint 0.
                yield return new WaitForSeconds(1.5f);
                Assert.AreEqual(1, race.Tracker.Get(0).CheckpointsPassed);
            }
            finally
            {
                Object.Destroy(screen.gameObject);
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AServerHostsWithNoCarOfItsOwn()
        {
            var screen = TrackScreen.CreateServer(() => NetworkSession.StartServer("Test server", TestPort + 2));
            try
            {
                yield return null;
                yield return null;

                var session = screen.Session;
                Assert.IsFalse(session.Ended, session.Status);
                Assert.IsTrue(session.IsHost);
                Assert.IsTrue(session.IsDedicated);
                Assert.IsFalse(session.TryGetLocalPlayerId(out _));
                Assert.AreEqual(0, session.Players.Count);
                Assert.IsNull(screen.Car, "a server has no car");
                Assert.IsFalse(session.LeadsRoom, "nobody runs an empty room");
                Assert.IsFalse(session.CanStart);
                Assert.IsNotNull(screen.GetComponent<HostLobby>());
                Assert.IsNull(screen.GetComponent<SessionOverlay>(), "nothing is drawn on a server");
            }
            finally
            {
                Object.Destroy(screen.gameObject);
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheStartCommandStartsTheEventTheSettingsSay()
        {
            var screen = TrackScreen.Create(new NoInput(), () => NetworkSession.StartHosting("Host", TestPort + 3));
            try
            {
                yield return null;
                yield return null;

                var session = screen.Session;
                var settings = LobbySettings.Default;
                settings.Laps = 2;
                settings.Track = 1;
                session.ChangeSettings(settings);
                session.Command(LobbyCommand.Start);

                var race = session.Race;
                Assert.AreEqual(RacePhase.Countdown, race.Phase);
                Assert.AreEqual(2, race.LapCount);
                Assert.AreEqual(1, race.TrackIndex);
            }
            finally
            {
                Object.Destroy(screen.gameObject);
            }

            yield return null;
            yield return null;
        }

        private sealed class FullThrottle : ICarInputSource
        {
            public CarInput Read() => new CarInput(0f, 1f);
        }
    }
}
