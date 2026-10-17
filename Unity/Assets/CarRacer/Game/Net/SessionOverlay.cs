using CarRacer.Core.Arena;
using CarRacer.Core.Bots;
using CarRacer.Core.Discovery;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Game.Driving;
using CarRacer.Game.Menu;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// The session screens drawn over the track (design doc §6, §11): the lobby (players, Ready,
    /// mode, laps and Start), the countdown, the race HUD (position and lap, or lives and who is
    /// left in an arena) and the results with a rematch. Temporary IMGUI until the real UI.
    /// </summary>
    public sealed class SessionOverlay : MonoBehaviour
    {
        public const int MaxLaps = 9;
        public const string BotFillKey = "botFill";
        public const string BotDifficultyKey = "botDifficulty";
        public const string ModeKey = "mode";

        /// <summary>The bot fill choices (design doc §9): off, or fill the grid to 4 or 6 cars.</summary>
        private static readonly int[] BotFills = { 0, 4, 6 };

        private NetworkSession _session;
        private NetworkedCars _cars;
        private int _laps = 3;
        private GameMode _mode = GameMode.Race;
        /// <summary>How often the host's own addresses are re-read, in case Wi-Fi or the hotspot comes up (or changes) after the lobby opens.</summary>
        private const float AddressRefreshSeconds = 3f;

        private string _hostAddresses = string.Empty;
        private float _nextAddressRefresh;
        private GUIStyle _small;
        private GUIStyle _big;

        public void Initialise(NetworkSession session, NetworkedCars cars)
        {
            _session = session;
            _cars = cars;
            if (session.IsHost)
            {
                session.HostSide.BotFill = PlayerPrefs.GetInt(BotFillKey, 0);
                session.HostSide.BotDifficulty = (BotDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(BotDifficultyKey, (int)BotDifficulty.Normal), 0, 2);
                _mode = (GameMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, (int)GameMode.Race), 0, 2);
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
                }

                if (_hostAddresses.Length > 0)
                {
                    GUILayout.Label(_hostAddresses, UiStyle.Hint);
                }

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
                DrawBotSettings();

                // The host decides (design doc §11); alone, it's a practice race.
                bool canStart = _session.HostSide.Roster.EveryoneReady || _session.HostSide.Roster.Count == 1;
                GUI.enabled = canStart;
                if (GUILayout.Button(canStart ? $"Start {ModeName(_mode)}" : "Waiting for everyone to be ready", UiStyle.PrimaryButton))
                {
                    ((HostRace)_session.Race).Start(_laps, _mode);
                }

                GUI.enabled = true;
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
            float x = width / 2f - 260f;
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
            if (_session.IsHost)
            {
                if (GUI.Button(new Rect(x, y, 220f, 48f), "Back to lobby"))
                {
                    ((HostRace)race).ReturnToLobby();
                }
            }
            else
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
