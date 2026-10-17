using System;
using System.Collections.Generic;
using CarRacer.Core.Net;

namespace CarRacer.Core.Session
{
    /// <summary>One person in the session, as the host tracks them.</summary>
    public sealed class LobbyPlayer
    {
        public byte PlayerId;
        public string Name = string.Empty;
        public byte ColourIndex;
        public bool Ready;
        public bool IsHost;
    }

    /// <summary>
    /// The host's list of players (design doc §11). Hands out the lowest free player id, which is
    /// also the grid slot until race rules decide otherwise, and the lowest free car colour, so
    /// no two players share one. The host is always ready; guests tap Ready.
    /// </summary>
    public sealed class LobbyRoster
    {
        public const int MaxPlayers = 6;
        public const int MaxNameChars = 24;

        /// <summary>Number of distinct car colours; at least <see cref="MaxPlayers"/>.</summary>
        public const int ColourCount = 8;

        private readonly List<LobbyPlayer> _players = new List<LobbyPlayer>();

        public IReadOnlyList<LobbyPlayer> Players => _players;
        public int Count => _players.Count;
        public bool IsFull => _players.Count >= MaxPlayers;

        /// <summary>True when there is at least one guest and every guest is ready.</summary>
        public bool EveryoneReady
        {
            get
            {
                bool anyGuest = false;
                foreach (var p in _players)
                {
                    if (!p.IsHost)
                    {
                        anyGuest = true;
                        if (!p.Ready)
                        {
                            return false;
                        }
                    }
                }

                return anyGuest;
            }
        }

        /// <summary>Whether the host should accept a guest's <see cref="JoinRequestMessage"/>.</summary>
        public JoinResult CheckJoin(ushort guestProtocolVersion, ushort hostProtocolVersion, bool eventInProgress)
        {
            if (guestProtocolVersion != hostProtocolVersion)
            {
                return JoinResult.VersionMismatch;
            }

            if (eventInProgress)
            {
                return JoinResult.EventInProgress;
            }

            return IsFull ? JoinResult.SessionFull : JoinResult.Accepted;
        }

        /// <summary>Adds a player, or returns null if the session is full.</summary>
        public LobbyPlayer Add(string name, bool isHost = false)
        {
            if (IsFull)
            {
                return null;
            }

            var player = new LobbyPlayer
            {
                PlayerId = LowestFree(p => p.PlayerId),
                Name = CleanName(name),
                ColourIndex = LowestFree(p => p.ColourIndex),
                Ready = isHost,
                IsHost = isHost,
            };

            _players.Add(player);
            _players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            return player;
        }

        public bool Remove(byte playerId) => _players.RemoveAll(p => p.PlayerId == playerId) > 0;

        public LobbyPlayer Find(byte playerId) => _players.Find(p => p.PlayerId == playerId);

        public bool SetReady(byte playerId, bool ready)
        {
            var player = Find(playerId);
            if (player == null || player.IsHost)
            {
                return false;
            }

            player.Ready = ready;
            return true;
        }

        /// <summary>Clears every guest's Ready, e.g. when an event ends and the lobby reopens.</summary>
        public void ResetReady()
        {
            foreach (var p in _players)
            {
                p.Ready = p.IsHost;
            }
        }

        public RosterMessage ToMessage()
        {
            var message = new RosterMessage();
            foreach (var p in _players)
            {
                message.Players.Add(new RosterEntry
                {
                    PlayerId = p.PlayerId,
                    Name = p.Name,
                    ColourIndex = p.ColourIndex,
                    Ready = p.Ready,
                    IsHost = p.IsHost,
                });
            }

            return message;
        }

        /// <summary>Trims the name and limits it to <see cref="MaxNameChars"/>; a blank name becomes "Player".</summary>
        public static string CleanName(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return "Player";
            }

            return trimmed.Length > MaxNameChars ? trimmed.Substring(0, MaxNameChars).TrimEnd() : trimmed;
        }

        private byte LowestFree(Func<LobbyPlayer, byte> taken)
        {
            for (int candidate = 0; candidate < byte.MaxValue; candidate++)
            {
                if (!_players.Exists(p => taken(p) == candidate))
                {
                    return (byte)candidate;
                }
            }

            throw new InvalidOperationException("No free value.");
        }
    }
}
