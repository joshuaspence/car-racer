using System.Collections;
using CarRacer.Core.Driving;
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
    }
}
