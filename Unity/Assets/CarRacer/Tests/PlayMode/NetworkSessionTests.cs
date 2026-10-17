using System.Collections;
using CarRacer.Core.Driving;
using CarRacer.Core.Race;
using CarRacer.Game.DriveTest;
using CarRacer.Game.Driving;
using CarRacer.Game.Net;
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
            var arena = DriveTestArena.Create(new NoInput(), () => NetworkSession.StartHosting("Test host", TestPort));
            try
            {
                yield return null;
                yield return null;

                var session = arena.Session;
                Assert.IsFalse(session.Ended, session.Status);
                Assert.IsTrue(session.IsHost);
                Assert.IsTrue(session.TryGetLocalPlayerId(out byte me));
                Assert.AreEqual(0, me);
                Assert.AreEqual(1, session.Players.Count);
                Assert.AreEqual("Test host", session.Players[0].Name);
                Assert.IsNotNull(arena.Car, "the host's car should be spawned on its grid slot");
            }
            finally
            {
                Object.Destroy(arena.gameObject);
            }

            // Let NGO shut down before anything else starts a NetworkManager.
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASoloRaceHoldsTheCarOnTheGridUntilGo()
        {
            var input = new FullThrottle();
            var arena = DriveTestArena.Create(input, () => NetworkSession.StartHosting("Solo", TestPort + 1));
            try
            {
                yield return null;
                yield return null;
                var race = (HostRace)arena.Session.Race;
                Assert.IsTrue(race.Start(laps: 1));

                yield return new WaitForSeconds(1f);
                Assert.AreEqual(RacePhase.Countdown, race.Phase);
                Assert.IsTrue(arena.Car.InputLocked);
                Assert.That(arena.Car.ForwardSpeed, Is.EqualTo(0f).Within(0.5f), "held on the grid despite full throttle");

                yield return new WaitForSeconds((HostRace.StartLeadMs + HostRace.CountdownMs) / 1000f);
                Assert.AreEqual(RacePhase.Racing, race.Phase);
                Assert.IsFalse(arena.Car.InputLocked);

                // Driving off the grid crosses the start line, checkpoint 0.
                yield return new WaitForSeconds(1.5f);
                Assert.AreEqual(1, race.Tracker.Get(0).CheckpointsPassed);
            }
            finally
            {
                Object.Destroy(arena.gameObject);
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
