using CarRacer.Core.Arena;
using CarRacer.Core.Art;
using CarRacer.Core.Bots;
using CarRacer.Core.Discovery;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Session;
using CarRacer.Core.Track;
using CarRacer.Game.Driving;
using CarRacer.Game.Menu;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// The session screens drawn over the track (design doc §6, §11): the lobby (players, Ready,
    /// mode, track, laps and Start), the countdown, the race HUD (position and lap, or lives and
    /// who is left in an arena) and the results with a rematch. Temporary IMGUI until the real UI.
    /// </summary>
    public sealed class SessionOverlay : MonoBehaviour
    {
        public const int MaxLaps = 9;
        public const string BotFillKey = "botFill";
        public const string BotDifficultyKey = "botDifficulty";
        public const string ModeKey = "mode";
        public const string TrackKey = "track";

        /// <summary>The bot fill choices (design doc §9): off, or fill the grid to 4 or 6 cars.</summary>
        private static readonly int[] BotFills = { 0, 4, 6 };

        private NetworkSession _session;
        private NetworkedCars _cars;
        private int _laps = 3;
        private GameMode _mode = GameMode.Race;
        private int _track;
        private CupPanel _cup;
        /// <summary>How often the host's own addresses are re-read, in case Wi-Fi or the hotspot comes up (or changes) after the lobby opens.</summary>
        private const float AddressRefreshSeconds = 3f;

        private string _hostAddresses = string.Empty;
        private string _joinLink;
        private Texture2D _joinQr;
        private float _nextAddressRefresh;
        private GUIStyle _small;
        private GUIStyle _big;

        public void Initialise(NetworkSession session, NetworkedCars cars)
        {
            _session = session;
            _cars = cars;
            _cup = new CupPanel(session);
            if (session.IsHost)
            {
                session.HostSide.BotFill = PlayerPrefs.GetInt(BotFillKey, 0);
                session.HostSide.BotDifficulty = (BotDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(BotDifficultyKey, (int)BotDifficulty.Normal), 0, 2);
                _mode = (GameMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, (int)GameMode.Race), 0, 2);
                _track = TrackCatalog.Clamp(PlayerPrefs.GetInt(TrackKey, 0));
            }
        }

        /// <summary>A session that has ended (host gone, connection lost) shows why, then goes back to the menu after this long.</summary>
        public const float EndedReturnSeconds = 4f;

        private void Update()
        {
            if (_session != null && _session.Ended && Time.realtimeSinceStartup - _session.EndedAt >= EndedReturnSeconds)
            {
                Bootstrap.ShowMainMenu(_session.Status);
                return;
            }

            if (_session != null && !_session.Ended && _session.Race != null)
            {
                _cup.Tick();
            }
        }

        private void OnGUI()
        {
            if (_session == null)
            {
                return;
            }

            UiStyle.Begin();
            float width = UiStyle.Width;
            float left = UiStyle.SafeArea.x + 24f;
            _small = _small ?? new GUIStyle(GUI.skin.label) { fontSize = 20 };
            _big = _big ?? new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(left, 64f, 700f, 30f), _session.Status, _small);
            if (_session.Ended || _session.Reconnecting)
            {
                // Design doc §12: "Reconnecting…" while the host is quiet; why it ended, before going back to the menu.
                var banner = new GUIStyle(_big) { fontSize = 56 };
                GUI.Label(new Rect(0f, 260f, width, 80f), _session.Ended ? _session.Status : "Reconnecting…", banner);
            }

            bool joined = _session.TryGetLocalPlayerId(out byte me);
            if (_session.Ended || !joined || _session.Race == null)
            {
                return;
            }

            var race = _session.Race;
            switch (race.Phase)
            {
                case RacePhase.Lobby:
                    DrawLobby(left, me);
                    break;
                case RacePhase.Countdown:
                case RacePhase.Racing:
                    DrawRace(race, width, me);
                    break;
                case RacePhase.Results:
                    DrawResults(race, width, me);
                    break;
            }
        }

        private void DrawLobby(float left, byte me)
        {
            GUILayout.BeginArea(new Rect(left, 100f, 540f, 600f));
            GUILayout.BeginVertical(UiStyle.Panel);
            GUILayout.Label("Lobby", UiStyle.Heading);
            foreach (var player in _session.Players)
            {
                string state = player.IsHost ? "host" : player.IsBot ? "bot" : player.Ready ? "ready" : "not ready";
                string you = player.PlayerId == me ? " (you)" : string.Empty;
                GUILayout.BeginHorizontal();
                var swatch = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f), GUILayout.Height(34f));
                Swatch(swatch.x, swatch.y + 2f, player.ColourIndex);
                GUILayout.Label(player.Name + you, UiStyle.Body);
                GUILayout.FlexibleSpace();
                GUILayout.Label(state, UiStyle.Hint);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            if (_session.IsHost)
            {
                // For guests whose network blocks discovery (design doc §1.1).
                if (Time.unscaledTime >= _nextAddressRefresh)
                {
                    var addresses = HostAddress.LocalIPv4Addresses();
                    _hostAddresses = addresses.Count > 0 ? "Join by IP: " + string.Join(" or ", addresses) : string.Empty;
                    _nextAddressRefresh = Time.unscaledTime + AddressRefreshSeconds;
                    UpdateJoinQr(addresses.Count > 0 ? JoinLink.Make(addresses[0], _session.Port) : null);
                }

                if (_hostAddresses.Length > 0)
                {
                    GUILayout.Label(_hostAddresses, UiStyle.Hint);
                }

                _cup.DrawLobbyChoice();
                if (!_cup.CupChosen)
                {
                    DrawSingleEventSettings();
                }

                DrawBotSettings();

                // The host decides (design doc §11); alone, it's a practice race.
                bool canStart = _session.HostSide.Roster.EveryoneReady || _session.HostSide.Roster.Count == 1;
                if (_cup.CupChosen)
                {
                    _cup.DrawStartButtons(canStart);
                }
                else
                {
                    GUI.enabled = canStart;
                    if (GUILayout.Button(canStart ? $"Start {ModeName(_mode)}" : "Waiting for everyone to be ready", UiStyle.PrimaryButton))
                    {
                        ((HostRace)_session.Race).Start(_laps, _mode, _track);
                    }

                    GUI.enabled = true;
                }
            }
            else
            {
                bool ready = _session.GuestSide.Ready;
                if (GUILayout.Button(ready ? "Not ready" : "Ready", ready ? UiStyle.Button : UiStyle.PrimaryButton))
                {
                    _session.GuestSide.SetReady(!ready);
                }

                GUILayout.Label(ready ? "Waiting for the host to start the race." : "Drive around, then tap Ready.", UiStyle.Hint);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Scanned with a guest's camera app, the code opens the game straight into joining (design doc §1.1).
            if (_session.IsHost && _joinQr != null)
            {
                var area = new Rect(left + 560f, 100f, 200f, 200f);
                GUI.DrawTexture(area, _joinQr);
                GUI.Label(new Rect(area.x, area.yMax + 4f, 240f, 30f), "Scan to join", UiStyle.Hint);
            }

            DrawCarPicker(new Rect(left + 560f, _session.IsHost ? 340f : 100f, 248f, 300f), me);
        }

        /// <summary>
        /// Changing car without leaving the lobby (design doc §11): arrows through the bodies and a
        /// swatch per colour. Colours other players have are drawn small and can't be picked. The
        /// car changes once the host's roster comes back with it.
        /// </summary>
        private void DrawCarPicker(Rect area, byte me)
        {
            if (!TryFind(me, out var mine))
            {
                return;
            }

            const int perRow = 4;
            GUILayout.BeginArea(area, UiStyle.Panel);
            GUILayout.Label("Your car", UiStyle.Hint);
            GUILayout.Label(CarModels.DisplayName(CarModels.ForIndex(mine.BodyIndex)), _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", UiStyle.Button))
            {
                _session.ChangeCar(new CarChoice((byte)((mine.BodyIndex + LobbyRoster.BodyCount - 1) % LobbyRoster.BodyCount), mine.ColourIndex));
            }

            if (GUILayout.Button(">", UiStyle.Button))
            {
                _session.ChangeCar(new CarChoice((byte)((mine.BodyIndex + 1) % LobbyRoster.BodyCount), mine.ColourIndex));
            }

            GUILayout.EndHorizontal();
            for (int row = 0; row * perRow < LobbyRoster.ColourCount; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * perRow; i < Mathf.Min(LobbyRoster.ColourCount, (row + 1) * perRow); i++)
                {
                    var cell = GUILayoutUtility.GetRect(44f, 44f, GUILayout.ExpandWidth(true));
                    bool chosen = i == mine.ColourIndex;
                    bool taken = !chosen && ColourTaken((byte)i, me);
                    if (chosen)
                    {
                        Fill(cell, Color.white);
                    }

                    Fill(Inset(cell, chosen ? 3f : taken ? 14f : 6f), CarColours.ForIndex(i));
                    if (!taken && !chosen && GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    {
                        _session.ChangeCar(new CarChoice(mine.BodyIndex, (byte)i));
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        private bool ColourTaken(byte colour, byte me)
        {
            foreach (var p in _session.Players)
            {
                if (p.PlayerId != me && p.ColourIndex == colour)
                {
                    return true;
                }
            }

            return false;
        }

        private static Rect Inset(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width - 2f * by, r.height - 2f * by);

        private static void Fill(Rect rect, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>The host's settings for a single event: mode, laps and track.</summary>
        private void DrawSingleEventSettings()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModeName(_mode), UiStyle.Button, GUILayout.Width(150f)))
            {
                _mode = (GameMode)(((int)_mode + 1) % 3);
                PlayerPrefs.SetInt(ModeKey, (int)_mode);
            }

            if (_mode == GameMode.Race)
            {
                GUILayout.Label($"Laps: {_laps}", UiStyle.Body, GUILayout.Width(130f));
                if (GUILayout.Button("-", UiStyle.Button, GUILayout.Width(80f)))
                {
                    _laps = Mathf.Max(1, _laps - 1);
                }

                if (GUILayout.Button("+", UiStyle.Button, GUILayout.Width(80f)))
                {
                    _laps = Mathf.Min(MaxLaps, _laps + 1);
                }
            }
            else
            {
                GUILayout.Label(_mode == GameMode.Sumo ? "Last car on the platform wins" : $"{BattleRules.StartingLives} lives, items only", UiStyle.Hint);
            }

            GUILayout.EndHorizontal();
            if (_mode == GameMode.Race)
            {
                // The host picks the track (design doc §11); everyone moves to it at the countdown.
                if (GUILayout.Button("Track: " + TrackCatalog.Layouts[_track].Definition.DisplayName, UiStyle.Button))
                {
                    _track = (_track + 1) % TrackCatalog.Count;
                    PlayerPrefs.SetInt(TrackKey, _track);
                }
            }
        }

        private void UpdateJoinQr(string link)
        {
            if (link == _joinLink)
            {
                return;
            }

            _joinLink = link;
            if (_joinQr != null)
            {
                Destroy(_joinQr);
                _joinQr = null;
            }

            if (link != null)
            {
                _joinQr = QrTexture.Build(link);
            }
        }

        private void OnDestroy()
        {
            if (_joinQr != null)
            {
                Destroy(_joinQr);
            }
        }

        /// <summary>The host's bot fill and difficulty (design doc §9), remembered between sessions.</summary>
        private void DrawBotSettings()
        {
            var host = _session.HostSide;
            GUILayout.BeginHorizontal();
            string fill = host.BotFill == 0 ? "Bots: off" : $"Bots: fill to {host.BotFill}";
            if (GUILayout.Button(fill, UiStyle.Button, GUILayout.Width(250f)))
            {
                int next = (System.Array.IndexOf(BotFills, host.BotFill) + 1) % BotFills.Length;
                host.BotFill = BotFills[next];
                PlayerPrefs.SetInt(BotFillKey, host.BotFill);
            }

            GUI.enabled = host.BotFill > 0;
            if (GUILayout.Button(host.BotDifficulty.ToString(), UiStyle.Button))
            {
                host.BotDifficulty = (BotDifficulty)(((int)host.BotDifficulty + 1) % 3);
                PlayerPrefs.SetInt(BotDifficultyKey, (int)host.BotDifficulty);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawRace(IRaceView race, float width, byte me)
        {
            long msToGo = race.StartAtLocalMs - NetworkSession.NowMs;
            if (race.Phase == RacePhase.Countdown)
            {
                if (msToGo <= HostRace.CountdownMs)
                {
                    GUI.Label(new Rect(0f, 200f, width, 140f), ((msToGo + 999) / 1000).ToString(), _big);
                }
            }
            else if (msToGo > -1000)
            {
                GUI.Label(new Rect(0f, 200f, width, 140f), "GO!", _big);
            }

            if (race.Mode != GameMode.Race)
            {
                DrawArenaStatus(race, width, me, msToGo);
                return;
            }

            int lap = Mathf.Min(_cars.Detector.LapsCompleted + 1, race.LapCount);
            string position = string.Empty;
            var standings = race.Standings;
            if (standings != null)
            {
                for (int i = 0; i < standings.Order.Count; i++)
                {
                    if (standings.Order[i].PlayerId == me)
                    {
                        var entry = standings.Order[i];
                        position = entry.FinishPosition > 0
                            ? $"Finished {RaceFormat.Ordinal(entry.FinishPosition)}"
                            : $"{RaceFormat.Ordinal(i + 1)} / {standings.Order.Count}";
                        if (entry.FinishPosition > 0)
                        {
                            lap = race.LapCount;
                        }
                    }
                }
            }

            var style = new GUIStyle(_small) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(width - 424f, 84f, 400f, 44f), position, style);
            GUI.Label(new Rect(width - 424f, 128f, 400f, 44f), $"Lap {lap} / {race.LapCount}", style);
            DrawItemSlot(width, style);
        }

        /// <summary>The arena HUD (design doc §7.0): this phone's lives (Battle) or place once out, how many are left, and Battle's clock.</summary>
        private void DrawArenaStatus(IRaceView race, float width, byte me, long msToGo)
        {
            int left = 0;
            int total = 0;
            string status = string.Empty;
            var standings = race.Standings;
            if (standings != null)
            {
                foreach (var entry in standings.Order)
                {
                    total++;
                    if (entry.FinishPosition == 0)
                    {
                        left++;
                    }

                    if (entry.PlayerId == me)
                    {
                        status = entry.FinishPosition > 0
                            ? $"Out: {RaceFormat.Ordinal(entry.FinishPosition)}"
                            : race.Mode == GameMode.Battle ? $"Lives {entry.LapsCompleted}" : "Still on";
                    }
                }
            }

            var style = new GUIStyle(_small) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(width - 424f, 84f, 400f, 44f), status, style);
            GUI.Label(new Rect(width - 424f, 128f, 400f, 44f), total > 0 ? $"{left} / {total} left" : string.Empty, style);
            if (race.Mode == GameMode.Battle && race.Phase == RacePhase.Racing)
            {
                long remaining = System.Math.Max(0L, BattleRules.TimeLimitMs + msToGo);
                GUI.Label(new Rect(width - 424f, 172f, 400f, 44f), RaceFormat.Time(remaining), style);
            }

            DrawItemSlot(width, style);
        }

        /// <summary>The item slot, top centre (design doc §6).</summary>
        private void DrawItemSlot(float width, GUIStyle style)
        {
            var held = _session.Items?.Held ?? ItemKind.None;
            var slot = new Rect(width / 2f - 110f, 16f, 220f, 56f);
            GUI.Box(slot, GUIContent.none, UiStyle.Panel);
            var slotStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter };
            slotStyle.normal.textColor = held == ItemKind.None ? UiStyle.Muted : UiStyle.Accent;
            GUI.Label(slot, held == ItemKind.None ? "—" : ItemNames.Label(held), slotStyle);
        }

        private void DrawResults(IRaceView race, float width, byte me)
        {
            var results = race.Results;

            // In a cup the table goes beside the results, and its buttons replace the lobby button.
            var cup = _cup.CurrentStandings(race);
            float x = cup != null ? width / 2f - 580f : width / 2f - 260f;
            if (cup != null)
            {
                _cup.DrawStandings(cup, width / 2f + 40f, 140f, TryFind(me, out var self) ? self.Name : string.Empty);
            }

            float y = 140f;
            GUI.Box(new Rect(x - 20f, y - 20f, 560f, 80f + 32f * results.Results.Count), GUIContent.none);
            GUI.Label(new Rect(x, y, 520f, 34f), "Results", new GUIStyle(_small) { fontSize = 30, fontStyle = FontStyle.Bold });
            y += 44f;
            foreach (var r in results.Results)
            {
                bool stillHere = TryFind(r.PlayerId, out var player);
                Swatch(x, y, stillHere ? player.ColourIndex : r.PlayerId);
                // In an arena the time is how long the player lasted.
                string time = r.DidNotFinish ? "DNF" : RaceFormat.Time(r.RaceTimeMs);
                string tag = r.PlayerId == me ? " (you)" : !stillHere ? " (left)" : player.IsBot ? " (bot)" : string.Empty;
                GUI.Label(new Rect(x + 28f, y, 360f, 30f), $"{RaceFormat.Ordinal(r.Position)}  {r.Name}{tag}", _small);
                GUI.Label(new Rect(x + 400f, y, 120f, 30f), time, _small);
                y += 32f;
            }

            y += 16f;
            if (_session.IsHost && _cup.Run == null)
            {
                if (GUI.Button(new Rect(x, y, 220f, 48f), "Back to lobby"))
                {
                    ((HostRace)race).ReturnToLobby();
                }
            }
            else if (!_session.IsHost && cup == null)
            {
                GUI.Label(new Rect(x, y, 520f, 30f), "Waiting for the host...", _small);
            }
        }

        private static string ModeName(GameMode mode) => mode == GameMode.Race ? "Race" : mode == GameMode.Sumo ? "Sumo" : "Battle";

        /// <summary>False if the player has left the session since the race began.</summary>
        private bool TryFind(byte playerId, out RosterEntry player)
        {
            foreach (var p in _session.Players)
            {
                if (p.PlayerId == playerId)
                {
                    player = p;
                    return true;
                }
            }

            player = default;
            return false;
        }

        private static void Swatch(float x, float y, int colourIndex)
        {
            var previous = GUI.color;
            GUI.color = CarColours.ForIndex(colourIndex);
            GUI.DrawTexture(new Rect(x, y + 5f, 16f, 16f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
