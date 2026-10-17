using System;
using System.Collections.Generic;
using CarRacer.Core.Discovery;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Session;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// A hosted or joined LAN session (design doc §1, §2.1). Owns an NGO NetworkManager with Unity
    /// Transport, created from code, and carries the Core session protocol over one NGO custom
    /// message. The protocol itself lives in <see cref="HostSession"/> and <see cref="GuestSession"/>;
    /// this class only moves bytes, tracks connection events and, while hosting, announces the
    /// session on the LAN. It lives on its own root object (NGO expects the NetworkManager at the
    /// root of the hierarchy); destroying that object shuts the session down.
    /// </summary>
    public sealed class NetworkSession : MonoBehaviour, ISessionTransport
    {
        public const string MessageName = "car-racer";

        /// <summary>Messages are a few dozen bytes; anything bigger than this is malformed.</summary>
        public const int MaxMessageBytes = 2048;

        /// <summary>A refused guest is disconnected after this long, so the reply explaining why reaches it first.</summary>
        public const float RefusalDisconnectDelaySeconds = 0.5f;

        /// <summary>Assumed one-way latency to guests on a LAN; the host has no clock samples of its own.</summary>
        public const float HostLatencyGuessMs = 15f;

        private readonly List<(ulong Connection, float AtTime)> _pendingDisconnects = new List<(ulong, float)>();
        private NetworkManager _network;
        private DiscoveryAnnouncer _announcer;
        private DiscoveryAnnouncement _announcement;

        public bool IsHost { get; private set; }
        public HostSession HostSide { get; private set; }
        public GuestSession GuestSide { get; private set; }

        /// <summary>A short line for the HUD: connecting, joined, refused, disconnected…</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>True once the session can't continue (failed to start, refused, or lost the host).</summary>
        public bool Ended { get; private set; }

        public event Action<CarStateMessage> CarStateReceived;

        /// <summary>A hit addressed to this phone's car: another car's fallback bump, or an item hit from the host.</summary>
        public event Action<HitMessage> HitReceived;

        /// <summary>The race referee (host) or follower (guest), once <see cref="CreateRace"/> has been called.</summary>
        public IRaceView Race { get; private set; }

        /// <summary>The item referee (host) or follower (guest), created with the race.</summary>
        public IItemView Items { get; private set; }

        public static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

        /// <summary>This phone's player id, once it has one.</summary>
        public bool TryGetLocalPlayerId(out byte playerId)
        {
            if (IsHost)
            {
                playerId = HostSide.HostPlayerId;
                return true;
            }

            playerId = GuestSide?.PlayerId ?? 0;
            return GuestSide != null && GuestSide.State == GuestState.Joined;
        }

        public IReadOnlyList<RosterEntry> Players => IsHost ? HostSide.Roster.ToMessage().Players : GuestSide.Roster.Players;

        /// <summary>One-way latency estimate for showing <paramref name="playerId"/>'s car (guest cars reach other guests via the host).</summary>
        public float LatencyMsFor(byte playerId)
        {
            if (IsHost)
            {
                return HostLatencyGuessMs;
            }

            float toHost = GuestSide.Clock.HasEstimate ? GuestSide.Clock.OneWayLatencyMs : HostLatencyGuessMs;
            bool fromHost = playerId == HostPlayerIdFromRoster();
            return fromHost ? toHost : 2f * toHost;
        }

        /// <summary>
        /// Sets up racing on a track with <paramref name="checkpointCount"/> checkpoints and item
        /// boxes at <paramref name="itemBoxes"/>. The host needs <paramref name="fractionToNextCheckpoint"/>
        /// (how far each player's car is towards its next checkpoint) for standings, and
        /// <paramref name="carPositions"/> (where it sees each car) for item hits.
        /// </summary>
        public void CreateRace(int checkpointCount, Func<int, float> fractionToNextCheckpoint, IReadOnlyList<Vec3> itemBoxes, CarPositionLookup carPositions)
        {
            if (IsHost)
            {
                var random = new System.Random();
                Race = new HostRace(HostSide, checkpointCount, () => NowMs, fractionToNextCheckpoint);
                Items = new HostItems(HostSide, Race, itemBoxes, () => NowMs, random.NextDouble, carPositions);
            }
            else
            {
                Race = new GuestRace(GuestSide, () => NowMs);
                Items = new GuestItems(GuestSide, Race, itemBoxes, () => NowMs);
            }
        }

        /// <summary>Sends a fallback bump (or, on the host, an item hit) to the car it hit.</summary>
        public void SendHit(HitMessage hit)
        {
            if (IsHost)
            {
                HostSide.SendHit(hit);
            }
            else
            {
                GuestSide.SendToHost(hit, reliable: true);
            }
        }

        public void SendCarState(CarStateMessage state)
        {
            if (IsHost)
            {
                HostSide.BroadcastCarState(state);
            }
            else
            {
                GuestSide.SendCarState(state);
            }
        }

        public static NetworkSession StartHosting(string playerName, ushort port = DiscoveryConstants.DefaultGamePort)
        {
            var session = Create();
            session.IsHost = true;
            session.HostSide = new HostSession(session, playerName, DiscoveryConstants.ProtocolVersion, () => NowMs);
            session.HostSide.CarStateReceived += session.RaiseCarState;
            session.HostSide.HitReceived += session.RaiseHit;

            var transport = session.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!session._network.StartHost())
            {
                session.Fail($"Couldn't host on port {port}");
                return session;
            }

            session.RegisterHandler();
            session._announcement = new DiscoveryAnnouncement
            {
                SessionId = (uint)new System.Random().Next(1, int.MaxValue),
                HostName = LobbyRoster.CleanName(playerName),
                GamePort = port,
                MaxPlayers = LobbyRoster.MaxPlayers,
            };
            session._announcer = new DiscoveryAnnouncer(DiscoveryAnnouncer.DefaultTargets());
            session.Status = "Hosting";
            return session;
        }

        public static NetworkSession Join(string playerName, string address, ushort port)
        {
            var session = Create();
            session.GuestSide = new GuestSession(session, playerName, DiscoveryConstants.ProtocolVersion, () => NowMs);
            session.GuestSide.CarStateReceived += session.RaiseCarState;
            session.GuestSide.HitReceived += session.RaiseHit;

            var transport = session.GetComponent<UnityTransport>();
            transport.SetConnectionData(address, port);
            if (!session._network.StartClient())
            {
                session.Fail($"Couldn't connect to {address}:{port}");
                return session;
            }

            session.RegisterHandler();
            session.Status = $"Connecting to {address}…";
            return session;
        }

        private static NetworkSession Create()
        {
            // Configure everything before NetworkManager.Awake runs.
            var go = new GameObject("Network session");
            go.SetActive(false);
            var transport = go.AddComponent<UnityTransport>();
            var network = go.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = false,
                EnableSceneManagement = false,
            };

            var session = go.AddComponent<NetworkSession>();
            session._network = network;
            network.OnClientConnectedCallback += session.OnClientConnected;
            network.OnClientDisconnectCallback += session.OnClientDisconnected;
            go.SetActive(true);
            return session;
        }

        // ISessionTransport: what HostSession and GuestSession use to talk.
        void ISessionTransport.Send(ulong connection, IGameMessage message, bool reliable)
        {
            if (_network == null || !_network.IsListening)
            {
                return;
            }

            byte[] bytes = MessageCodec.Encode(message);
            using (var writer = new FastBufferWriter(bytes.Length + sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(bytes.Length);
                writer.WriteBytesSafe(bytes);
                var delivery = reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.Unreliable;
                _network.CustomMessagingManager.SendNamedMessage(MessageName, connection, writer, delivery);
            }
        }

        void ISessionTransport.Disconnect(ulong connection)
        {
            _pendingDisconnects.Add((connection, Time.realtimeSinceStartup + RefusalDisconnectDelaySeconds));
        }

        private void RegisterHandler()
        {
            _network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
        }

        private void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            IGameMessage message;
            try
            {
                reader.ReadValueSafe(out int length);
                if (length <= 0 || length > MaxMessageBytes)
                {
                    return;
                }

                var bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                message = MessageCodec.Decode(bytes);
            }
            catch (Exception e) when (e is NetFormatException || e is OverflowException || e is IndexOutOfRangeException)
            {
                Debug.LogWarning($"Dropped a malformed message from client {sender}: {e.Message}");
                return;
            }

            if (IsHost)
            {
                HostSide.OnMessage(sender, message);
            }
            else
            {
                GuestSide.OnMessage(message);
                if (GuestSide.State == GuestState.Refused)
                {
                    Fail(RefusalText(GuestSide.RefusalReason));
                }
                else if (GuestSide.State == GuestState.Joined)
                {
                    Status = "Joined";
                }
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsHost && clientId == _network.LocalClientId)
            {
                Status = "Joining…";
                GuestSide.OnConnected();
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsHost)
            {
                HostSide.OnDisconnected(clientId);
            }
            else if (!Ended)
            {
                Fail(GuestSide.State == GuestState.Joined ? "The host left" : "Couldn't reach the host");
            }
        }

        private void RaiseCarState(CarStateMessage state) => CarStateReceived?.Invoke(state);

        private void RaiseHit(HitMessage hit) => HitReceived?.Invoke(hit);

        private void Update()
        {
            long now = NowMs;
            GuestSide?.Tick();
            if (!Ended)
            {
                Race?.Tick();
                Items?.Tick();
            }

            if (_announcer != null)
            {
                _announcement.PlayerCount = (byte)HostSide.Roster.Count;
                _announcement.State = HostSide.EventInProgress ? HostSessionState.InEvent : HostSessionState.Lobby;
                _announcer.Tick(_announcement, now);
            }

            for (int i = _pendingDisconnects.Count - 1; i >= 0; i--)
            {
                if (Time.realtimeSinceStartup >= _pendingDisconnects[i].AtTime)
                {
                    if (_network != null && _network.IsServer)
                    {
                        _network.DisconnectClient(_pendingDisconnects[i].Connection);
                    }

                    _pendingDisconnects.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            _announcer?.Dispose();
            _announcer = null;
            if (_network != null)
            {
                _network.OnClientConnectedCallback -= OnClientConnected;
                _network.OnClientDisconnectCallback -= OnClientDisconnected;
                if (_network.IsListening)
                {
                    _network.Shutdown();
                }
            }
        }

        private void Fail(string reason)
        {
            Ended = true;
            Status = reason;
            _announcer?.Dispose();
            _announcer = null;
        }

        private byte HostPlayerIdFromRoster()
        {
            foreach (var p in GuestSide.Roster.Players)
            {
                if (p.IsHost)
                {
                    return p.PlayerId;
                }
            }

            return 0;
        }

        private static string RefusalText(JoinResult result)
        {
            switch (result)
            {
                case JoinResult.SessionFull:
                    return "That game is full";
                case JoinResult.VersionMismatch:
                    return "That game needs a different version of Car Racer";
                case JoinResult.EventInProgress:
                    return "That game has already started";
                default:
                    return "Couldn't join";
            }
        }
    }
}
