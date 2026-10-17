using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Game.Driving;
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
        private const float ReferenceHeight = 720f;

        private NetworkSession _session;
        private NetworkedCars _cars;
        private int _laps = 3;
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

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float left = Screen.safeArea.x / scale + 24f;
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
            float y = 96f;
            foreach (var player in _session.Players)
            {
                string tags = (player.IsHost ? " (host)" : player.Ready ? " - ready" : " - not ready") + (player.PlayerId == me ? " (you)" : string.Empty);
                Swatch(left, y, player.ColourIndex);
                GUI.Label(new Rect(left + 28f, y, 500f, 26f), player.Name + tags, _small);
                y += 26f;
            }

            y += 12f;
            if (_session.IsHost)
            {
                var host = (HostRace)_session.Race;
                GUI.Label(new Rect(left, y + 8f, 80f, 30f), $"Laps: {_laps}", _small);
                if (GUI.Button(new Rect(left + 90f, y, 52f, 44f), "-"))
                {
                    _laps = Mathf.Max(1, _laps - 1);
                }

                if (GUI.Button(new Rect(left + 148f, y, 52f, 44f), "+"))
                {
                    _laps = Mathf.Min(MaxLaps, _laps + 1);
                }

                // The host decides (design doc §11); alone, it's a practice race.
                bool canStart = _session.HostSide.Roster.EveryoneReady || _session.HostSide.Roster.Count == 1;
                GUI.enabled = canStart;
                if (GUI.Button(new Rect(left + 220f, y, 180f, 44f), canStart ? "Start race" : "Waiting for Ready"))
                {
                    host.Start(_laps);
                }

                GUI.enabled = true;
            }
            else
            {
                bool ready = _session.GuestSide.Ready;
                if (GUI.Button(new Rect(left, y, 180f, 44f), ready ? "Not ready" : "Ready"))
                {
                    _session.GuestSide.SetReady(!ready);
                }
            }
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
