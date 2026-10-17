using System;
using System.Net.Sockets;
using CarRacer.Core.Discovery;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Discovery
{
    /// <summary>
    /// Temporary start screen for testing on real phones: "Host game" starts a session that the
    /// other phones see listed within a second or two, and "Join" connects to one. Both open a free
    /// drive with everyone in the session. "Drive test" drives alone. Replaced by the real lobby UI in M1.
    /// </summary>
    public sealed class DiscoveryTestScreen : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;

        private readonly DiscoveredHostList _hosts = new DiscoveredHostList();
        private readonly AndroidMulticastLock _multicastLock = new AndroidMulticastLock();
        private DiscoveryListener _listener;
        private string _playerName;
        private string _joinAddress = string.Empty;
        private string _joinError = string.Empty;
        private string _status = string.Empty;
        private Vector2 _scroll;

        private static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

        private void OnEnable()
        {
            _playerName = PlayerPrefs.GetString("playerName", SystemInfo.deviceModel);
            _joinAddress = PlayerPrefs.GetString("joinAddress", string.Empty);
            _multicastLock.Acquire();
            try
            {
                _listener = new DiscoveryListener();
                _status = _listener.JoinedMulticast ? "Looking for games (multicast + broadcast)" : "Looking for games (broadcast only)";
            }
            catch (SocketException e)
            {
                _status = $"Could not listen on port {DiscoveryConstants.DiscoveryPort}: {e.Message}";
            }
        }

        private void OnDisable()
        {
            _listener?.Dispose();
            _listener = null;
            _multicastLock.Release();
        }

        private void Update()
        {
            _listener?.Poll(_hosts, NowMs);
        }

        private void SaveName()
        {
            PlayerPrefs.SetString("playerName", _playerName);
        }

        private void OnGUI()
        {
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            var safe = Screen.safeArea;
            float left = safe.x / scale + 24f;

            GUI.skin.label.fontSize = 22;
            GUI.skin.button.fontSize = 24;
            GUI.skin.textField.fontSize = 24;

            GUILayout.BeginArea(new Rect(left, 24f, width - left - 24f, ReferenceHeight - 48f));
            GUILayout.Label($"Car Racer (protocol v{DiscoveryConstants.ProtocolVersion})");
            GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(90f));
            _playerName = GUILayout.TextField(_playerName ?? string.Empty, DiscoveryConstants.MaxHostNameChars, GUILayout.Width(320f), GUILayout.Height(44f));
            if (GUILayout.Button("Host game", GUILayout.Width(180f), GUILayout.Height(44f)))
            {
                SaveName();
                Bootstrap.HostGame(_playerName);
            }

            if (GUILayout.Button("Drive test", GUILayout.Width(180f), GUILayout.Height(44f)))
            {
                Bootstrap.ShowDriveTest();
            }

            GUILayout.EndHorizontal();

            // For networks that block discovery: type the address shown in the host's lobby.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Join by IP:", GUILayout.Width(130f));
            _joinAddress = GUILayout.TextField(_joinAddress ?? string.Empty, 21, GUILayout.Width(280f), GUILayout.Height(44f));
            if (GUILayout.Button("Join", GUILayout.Width(120f), GUILayout.Height(44f)))
            {
                if (HostAddress.TryParse(_joinAddress, out string address, out ushort port))
                {
                    SaveName();
                    PlayerPrefs.SetString("joinAddress", _joinAddress.Trim());
                    Bootstrap.JoinGame(_playerName, address, port);
                }
                else
                {
                    _joinError = "Type the address shown on the host's screen, like 192.168.1.23";
                }
            }

            GUILayout.Label(_joinError);
            GUILayout.EndHorizontal();

            GUILayout.Space(16f);
            GUILayout.Label($"Games found: {_hosts.Hosts.Count}");

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var host in _hosts.Hosts)
            {
                var a = host.Announcement;
                bool open = host.IsCompatible && !host.IsFull && a.State == HostSessionState.Lobby;
                string state = !host.IsCompatible
                    ? $"Update required (v{a.ProtocolVersion})"
                    : host.IsFull ? "Full" : a.State == HostSessionState.InEvent ? "Racing" : "Open";
                long age = Math.Max(0, NowMs - host.LastSeenMs);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"• {a.HostName} — {host.Address}:{a.GamePort} — {a.PlayerCount}/{a.MaxPlayers} — {state} — seen {age} ms ago");
                GUI.enabled = open;
                if (GUILayout.Button("Join", GUILayout.Width(120f), GUILayout.Height(40f)))
                {
                    SaveName();
                    Bootstrap.JoinGame(_playerName, host.Address, a.GamePort);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
