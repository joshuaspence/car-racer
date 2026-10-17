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
    /// laps and Start), the countdown, the race HUD (position and lap) and the results with a
    /// rematch. Temporary IMGUI until the real UI later in M1.
    /// </summary>
    public sealed class SessionOverlay : MonoBehaviour
    {
        public const int MaxLaps = 9;

        private NetworkSession _session;
        private NetworkedCars _cars;
        private int _laps = 3;
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
                string state = player.IsHost ? "host" : player.Ready ? "ready" : "not ready";
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
                GUILayout.Label($"Laps: {_laps}", UiStyle.Body, GUILayout.Width(150f));
                if (GUILayout.Button("-", UiStyle.Button, GUILayout.Width(80f)))
                {
                    _laps = Mathf.Max(1, _laps - 1);
                }

                if (GUILayout.Button("+", UiStyle.Button, GUILayout.Width(80f)))
                {
                    _laps = Mathf.Min(MaxLaps, _laps + 1);
                }

                GUILayout.EndHorizontal();

                // The host decides (design doc §11); alone, it's a practice race.
                bool canStart = _session.HostSide.Roster.EveryoneReady || _session.HostSide.Roster.Count == 1;
                GUI.enabled = canStart;
                if (GUILayout.Button(canStart ? "Start race" : "Waiting for everyone to be ready", UiStyle.PrimaryButton))
                {
                    ((HostRace)_session.Race).Start(_laps);
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

            // The item slot, top centre (design doc §6).
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
                string time = r.DidNotFinish ? "DNF" : RaceFormat.Time(r.RaceTimeMs);
                string tag = r.PlayerId == me ? " (you)" : stillHere ? string.Empty : " (left)";
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
