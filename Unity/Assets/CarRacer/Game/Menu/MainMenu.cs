using System.Net.Sockets;
using CarRacer.Core.Discovery;
using CarRacer.Core.Session;
using CarRacer.Game.Audio;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Menu
{
    /// <summary>
    /// The menus before a game (design doc §11): your name on first launch, then Host game, Join game
    /// and Practice. The Join page lists the games announced on this Wi-Fi as cards, and takes an IP
    /// address for networks that block discovery. Discovery and the Android multicast lock run only
    /// while the Join page is open (design doc §1.1).
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public const string NameKey = "playerName";
        public const string JoinAddressKey = "joinAddress";

        private enum Page
        {
            Name,
            Main,
            Join,
        }

        private readonly DiscoveredHostList _hosts = new DiscoveredHostList();
        private readonly AndroidMulticastLock _multicastLock = new AndroidMulticastLock();
        private DiscoveryListener _listener;
        private Page _page;
        private string _name;
        private string _nameDraft;
        private string _joinAddress;
        private string _joinMessage = string.Empty;
        private Vector2 _scroll;

        /// <summary>Shown on the main page, e.g. why the last session ended.</summary>
        public string Notice { get; set; }

        private static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

        private void OnEnable()
        {
            _name = PlayerPrefs.GetString(NameKey, string.Empty);
            _nameDraft = string.IsNullOrEmpty(_name) ? SystemInfo.deviceModel : _name;
            _joinAddress = PlayerPrefs.GetString(JoinAddressKey, string.Empty);
            _page = string.IsNullOrEmpty(_name) ? Page.Name : Page.Main;
        }

        private void OnDisable()
        {
            StopDiscovery();
        }

        private void Update()
        {
            if (_listener != null)
            {
                long now = NowMs;
                _listener.Poll(_hosts, now);
                _hosts.Expire(now);
            }
        }

        private void GoTo(Page page)
        {
            if (page == Page.Join && _page != Page.Join)
            {
                StartDiscovery();
            }
            else if (page != Page.Join && _page == Page.Join)
            {
                StopDiscovery();
            }

            _page = page;
        }

        private void StartDiscovery()
        {
            _multicastLock.Acquire();
            try
            {
                _listener = new DiscoveryListener();
                _joinMessage = string.Empty;
            }
            catch (SocketException e)
            {
                _joinMessage = $"Can't look for games on this network ({e.Message}). You can still join by IP.";
            }
        }

        private void StopDiscovery()
        {
            _listener?.Dispose();
            _listener = null;
            _multicastLock.Release();
        }

        private void OnGUI()
        {
            UiStyle.Begin();
            switch (_page)
            {
                case Page.Name:
                    DrawNamePage();
                    break;
                case Page.Main:
                    DrawMainPage();
                    break;
                case Page.Join:
                    DrawJoinPage();
                    break;
            }
        }

        private void DrawNamePage()
        {
            GUILayout.BeginArea(UiStyle.CentredPanel(560f, 120f, 480f), UiStyle.Panel);
            GUILayout.Label("Car Racer", UiStyle.Title);
            GUILayout.Space(16f);
            GUILayout.Label("What should other players call you?", UiStyle.Body);
            _nameDraft = GUILayout.TextField(_nameDraft ?? string.Empty, LobbyRoster.MaxNameChars, UiStyle.Field);
            GUILayout.Space(12f);

            GUI.enabled = !string.IsNullOrWhiteSpace(_nameDraft);
            if (GUILayout.Button("Continue", UiStyle.PrimaryButton))
            {
                _name = LobbyRoster.CleanName(_nameDraft);
                PlayerPrefs.SetString(NameKey, _name);
                GoTo(Page.Main);
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawMainPage()
        {
            GUILayout.BeginArea(UiStyle.CentredPanel(480f, 70f, 600f), UiStyle.Panel);
            GUILayout.Label("Car Racer", UiStyle.Title);
            GUILayout.Label($"Racing as {_name}", UiStyle.HintCentred);
            if (!string.IsNullOrEmpty(Notice))
            {
                GUILayout.Label(Notice, UiStyle.HintCentred);
            }

            GUILayout.Space(20f);

            if (GUILayout.Button("Host game", UiStyle.PrimaryButton))
            {
                Bootstrap.HostGame(_name);
            }

            if (GUILayout.Button("Join game", UiStyle.Button))
            {
                GoTo(Page.Join);
            }

            if (GUILayout.Button("Practice", UiStyle.Button))
            {
                Bootstrap.ShowPractice();
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("Change name", UiStyle.Button))
            {
                _nameDraft = _name;
                GoTo(Page.Name);
            }

            // Graphics is automatic by default (design doc §14); tap to choose a level instead.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GraphicsQuality.Label, UiStyle.Button))
            {
                GraphicsQuality.Cycle();
            }

            GUILayout.Space(12f);
            if (GUILayout.Button(GameAudio.Label, UiStyle.Button))
            {
                GameAudio.CycleVolume();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawJoinPage()
        {
            GUILayout.BeginArea(UiStyle.CentredPanel(760f, 24f, 680f), UiStyle.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Join a game", UiStyle.Heading);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", UiStyle.Button, GUILayout.Width(140f)))
            {
                GoTo(Page.Main);
            }

            GUILayout.EndHorizontal();

            var hosts = _hosts.Hosts;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (hosts.Count == 0)
            {
                GUILayout.Label(_listener != null ? "Looking for games on this Wi-Fi…" : string.Empty, UiStyle.Hint);
            }

            foreach (var host in hosts)
            {
                var a = host.Announcement;
                GUILayout.BeginHorizontal(UiStyle.Panel);
                GUILayout.BeginVertical();
                GUILayout.Label(a.HostName, UiStyle.Body);
                GUILayout.Label($"{a.PlayerCount}/{a.MaxPlayers} players · {host.StatusText}", UiStyle.Hint);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = host.CanJoin;
                if (GUILayout.Button("Join", UiStyle.PrimaryButton, GUILayout.Width(160f)))
                {
                    Bootstrap.JoinGame(_name, host.Address, a.GamePort);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            // For networks that block discovery: the address is shown in the host's lobby.
            GUILayout.Label("Not listed? Type the address shown in the host's lobby:", UiStyle.Hint);
            GUILayout.BeginHorizontal();
            _joinAddress = GUILayout.TextField(_joinAddress ?? string.Empty, 21, UiStyle.Field);
            if (GUILayout.Button("Join", UiStyle.Button, GUILayout.Width(160f)))
            {
                if (HostAddress.TryParse(_joinAddress, out string address, out ushort port))
                {
                    PlayerPrefs.SetString(JoinAddressKey, _joinAddress.Trim());
                    Bootstrap.JoinGame(_name, address, port);
                }
                else
                {
                    _joinMessage = "That isn't an address. It looks like 192.168.1.23.";
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(_joinMessage, UiStyle.Hint);
            GUILayout.EndArea();
        }
    }
}
