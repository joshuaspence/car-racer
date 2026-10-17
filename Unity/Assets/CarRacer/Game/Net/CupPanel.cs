using System;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Scoring;
using CarRacer.Game.Menu;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// The cup parts of the session screens (design doc §10). On the host: the lobby's choice of a
    /// single event or a cup, reverse grid, resuming a saved cup, the <see cref="CupRun"/> itself
    /// (ticked every frame) and its auto-save in PlayerPrefs. On every phone: the standings table
    /// beside the results, with the countdown to the next event, and the host's pause and skip.
    /// </summary>
    public sealed class CupPanel
    {
        public const string ChoiceKey = "cupChoice";
        public const string ReverseGridKey = "reverseGrid";
        public const string SaveKey = "cupSave";

        private readonly NetworkSession _session;

        /// <summary>0 is a single event; otherwise <see cref="Cups.All"/>[_choice - 1].</summary>
        private int _choice;
        private bool _reverseGrid = true;
        private string _savedText;
        private CupRun _saved;
        private GUIStyle _row;
        private GUIStyle _title;

        public CupPanel(NetworkSession session)
        {
            _session = session;
            if (session.IsHost)
            {
                _choice = Mathf.Clamp(PlayerPrefs.GetInt(ChoiceKey, 0), 0, Cups.All.Count);
                _reverseGrid = PlayerPrefs.GetInt(ReverseGridKey, 1) == 1;
            }
        }

        /// <summary>The host's cup in progress, or null.</summary>
        public CupRun Run { get; private set; }

        /// <summary>True when the host's lobby is set up for a cup rather than a single event.</summary>
        public bool CupChosen => _choice > 0;

        /// <summary>Call every frame.</summary>
        public void Tick() => Run?.Tick();

        /// <summary>The host's lobby choices: single event or a cup, and for a cup, reverse grid and resuming a save.</summary>
        public void DrawLobbyChoice()
        {
            string label = _choice == 0 ? "Event: single" : "Event: " + Cups.All[_choice - 1].Name;
            if (GUILayout.Button(label, UiStyle.Button))
            {
                _choice = (_choice + 1) % (Cups.All.Count + 1);
                PlayerPrefs.SetInt(ChoiceKey, _choice);
            }

            if (!CupChosen)
            {
                return;
            }

            var cup = Cups.All[_choice - 1];
            GUILayout.Label($"{cup.Events.Count} events; the next starts 10 s after each finishes.", UiStyle.Hint);
            if (GUILayout.Button(_reverseGrid ? "Reverse grid: on" : "Reverse grid: off", UiStyle.Button))
            {
                _reverseGrid = !_reverseGrid;
                PlayerPrefs.SetInt(ReverseGridKey, _reverseGrid ? 1 : 0);
            }
        }

        /// <summary>The host's start buttons for a cup: start a new one, or resume the saved one. Returns true if an event started.</summary>
        public bool DrawStartButtons(bool canStart)
        {
            var host = (HostRace)_session.Race;
            var saved = LoadSave(host);
            GUI.enabled = canStart;
            bool started = false;
            if (saved != null && GUILayout.Button($"Resume {saved.Cup.Name} ({saved.Championship.EventsCompleted} of {saved.Cup.Events.Count} done)", UiStyle.Button))
            {
                started = Begin(saved);
            }

            var cup = Cups.All[_choice - 1];
            if (GUILayout.Button(canStart ? "Start " + cup.Name : "Waiting for everyone to be ready", UiStyle.PrimaryButton))
            {
                started = Begin(new CupRun(_session.HostSide, host, () => NetworkSession.NowMs, cup, _reverseGrid));
            }

            GUI.enabled = true;
            return started;
        }

        /// <summary>The cup table for the results screen, or null outside a cup.</summary>
        public CupStandingsMessage CurrentStandings(IRaceView race)
        {
            if (race.Phase != RacePhase.Results)
            {
                return null;
            }

            return _session.IsHost ? Run?.Standings : (race as GuestRace)?.CupStandings;
        }

        /// <summary>
        /// Draws the cup table at (<paramref name="x"/>, <paramref name="y"/>) with the countdown to
        /// the next event, and on the host the pause, skip and finish buttons. Returns true once the
        /// host has finished with a completed cup (the caller goes back to the lobby).
        /// </summary>
        public bool DrawStandings(CupStandingsMessage table, float x, float y, string me)
        {
            _row = _row ?? new GUIStyle(GUI.skin.label) { fontSize = 20 };
            _title = _title ?? new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            const float width = 420f;
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 150f + 30f * table.Standings.Count), GUIContent.none);
            string heading = table.IsComplete ? $"{table.CupName}: final" : $"{table.CupName}: {table.EventsCompleted} of {table.TotalEvents}";
            GUI.Label(new Rect(x, y, width, 34f), heading, _title);
            y += 44f;
            for (int i = 0; i < table.Standings.Count; i++)
            {
                var entry = table.Standings[i];
                string you = string.Equals(entry.Name, me, StringComparison.OrdinalIgnoreCase) ? " (you)" : string.Empty;
                GUI.Label(new Rect(x, y, 300f, 28f), $"{RaceFormat.Ordinal(i + 1)}  {entry.Name}{you}", _row);
                GUI.Label(new Rect(x + 300f, y, 120f, 28f), $"{entry.Points} pts", _row);
                y += 30f;
            }

            y += 12f;
            if (table.IsComplete)
            {
                GUI.Label(new Rect(x, y, width, 30f), table.Standings.Count > 0 ? $"{table.Standings[0].Name} wins the {table.CupName}!" : string.Empty, _row);
                y += 36f;
                return _session.IsHost && GUI.Button(new Rect(x, y, 220f, 48f), "Back to lobby") && Finish();
            }

            long nextAt = table.NextEventAtHostMs;
            if (nextAt > 0 && !_session.IsHost)
            {
                nextAt = _session.GuestSide.Clock.ToLocalTime(nextAt);
            }

            long msToGo = nextAt - NetworkSession.NowMs;
            GUI.Label(new Rect(x, y, width, 30f), nextAt > 0 ? $"Next event in {Math.Max(0L, (msToGo + 999) / 1000)} s" : "Next event: waiting for the host", _row);
            y += 36f;
            if (_session.IsHost && Run != null)
            {
                if (GUI.Button(new Rect(x, y, 140f, 48f), Run.Paused ? "Resume" : "Pause"))
                {
                    Run.SetPaused(!Run.Paused);
                }

                if (GUI.Button(new Rect(x + 150f, y, 140f, 48f), "Next event"))
                {
                    Run.AdvanceNow();
                }

                // Leaving keeps the save, so the cup can be resumed from the lobby.
                if (GUI.Button(new Rect(x + 300f, y, 120f, 48f), "Leave cup"))
                {
                    Run = null;
                    ((HostRace)_session.Race).ReturnToLobby();
                }
            }

            return false;
        }

        private bool Begin(CupRun run)
        {
            if (!run.StartNextEvent())
            {
                return false;
            }

            run.Saved += Save;
            Run = run;
            _savedText = null;
            return true;
        }

        /// <summary>The finished cup is done with: its save goes, and everyone goes back to the lobby.</summary>
        private bool Finish()
        {
            Run = null;
            PlayerPrefs.DeleteKey(SaveKey);
            ((HostRace)_session.Race).ReturnToLobby();
            return true;
        }

        private static void Save(byte[] save)
        {
            PlayerPrefs.SetString(SaveKey, Convert.ToBase64String(save));
            PlayerPrefs.Save();
        }

        /// <summary>The saved cup, read again only when the save changes (this is called every frame in the lobby).</summary>
        private CupRun LoadSave(HostRace host)
        {
            string text = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (text == _savedText)
            {
                return _saved;
            }

            _savedText = text;
            _saved = null;
            if (text.Length > 0)
            {
                try
                {
                    _saved = CupRun.Resume(Convert.FromBase64String(text), _session.HostSide, host, () => NetworkSession.NowMs);
                }
                catch (FormatException)
                {
                }
            }

            return _saved;
        }
    }
}
