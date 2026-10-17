using System;
using System.Net.Sockets;
using CarRacer.Core.Discovery;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Discovery
{
    /// <summary>
    /// Temporary PR 1 screen for checking LAN discovery on real phones: one phone taps
    /// "Host", the others should see it listed within a second or two. "Drive test" opens the
    /// car tuning arena. Replaced by the real lobby UI in M1.
    /// </summary>
    public sealed class DiscoveryTestScreen : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;

        private readonly DiscoveredHostList _hosts = new DiscoveredHostList();
        private readonly AndroidMulticastLock _multicastLock = new AndroidMulticastLock();
        private DiscoveryListener _listener;
        private DiscoveryAnnouncer _announcer;
        private DiscoveryAnnouncement _announcement;
        private string _playerName;
        private string _status = string.Empty;
        private Vector2 _scroll;

        private static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

        private void OnEnable()
        {
            _playerName = PlayerPrefs.GetString("playerName", SystemInfo.deviceModel);
            _multicastLock.Acquire();
            try
            {
                _listener = new DiscoveryListener();
                _status = _listener.JoinedMulticast ? "Listening (multicast + broadcast)" : "Listening (broadcast only)";
            }
            catch (SocketException e)
            {
                _status = $"Could not listen on port {DiscoveryConstants.DiscoveryPort}: {e.Message}";
            }
        }

        private void OnDisable()
        {
            StopHosting();
            _listener?.Dispose();
            _listener = null;
            _multicastLock.Release();
        }

        private void Update()
        {
            long now = NowMs;
            _listener?.Poll(_hosts, now);
            if (_announcer != null)
            {
                _announcement.HostName = _playerName;
                _announcer.Tick(_announcement, now);
            }
        }

        private void StartHosting()
        {
            PlayerPrefs.SetString("playerName", _playerName);
            _announcement = new DiscoveryAnnouncement
            {
                SessionId = (uint)new System.Random().Next(1, int.MaxValue),
                HostName = _playerName,
                PlayerCount = 1,
            };
            _announcer = new DiscoveryAnnouncer(DiscoveryAnnouncer.DefaultTargets());
            _status = $"Hosting; announcing to {_announcer.Targets.Count} addresses";
        }

        private void StopHosting()
        {
            _announcer?.Dispose();
            _announcer = null;
            _announcement = null;
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
            GUILayout.Label($"Car Racer — LAN discovery test (protocol v{DiscoveryConstants.ProtocolVersion})");
            GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(90f));
            _playerName = GUILayout.TextField(_playerName ?? string.Empty, DiscoveryConstants.MaxHostNameChars, GUILayout.Width(320f), GUILayout.Height(44f));
            if (_announcer == null)
            {
                if (GUILayout.Button("Host", GUILayout.Width(180f), GUILayout.Height(44f)))
                {
                    StartHosting();
                }
            }
            else if (GUILayout.Button("Stop hosting", GUILayout.Width(180f), GUILayout.Height(44f)))
            {
                StopHosting();
                _status = "Stopped hosting";
            }

            if (GUILayout.Button("Drive test", GUILayout.Width(180f), GUILayout.Height(44f)))
            {
                Bootstrap.ShowDriveTest();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(16f);
            GUILayout.Label($"Games found: {_hosts.Hosts.Count}");

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var host in _hosts.Hosts)
            {
                var a = host.Announcement;
                string you = _announcement != null && host.SessionId == _announcement.SessionId ? " (you)" : string.Empty;
                string state = !host.IsCompatible
                    ? $"Update required (v{a.ProtocolVersion})"
                    : host.IsFull ? "Full" : a.State == HostSessionState.InEvent ? "Racing" : "Open";
                long age = Math.Max(0, NowMs - host.LastSeenMs);
                GUILayout.Label($"• {a.HostName}{you} — {host.Address}:{a.GamePort} — {a.PlayerCount}/{a.MaxPlayers} — {state} — seen {age} ms ago");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
