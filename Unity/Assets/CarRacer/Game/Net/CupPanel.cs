using System;
using System.Text;
using CarRacer.Core.Audio;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Scoring;
using CarRacer.Game.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// The cup parts of the session screens (design doc §10). On the host: the lobby's choice of a
    /// single event or a cup, reverse grid, resuming a saved cup, the <see cref="CupRun"/> itself
    /// (ticked every frame) and its auto-save in PlayerPrefs. On every phone: the standings table
    /// beside the results, with the countdown to the next event, and the host's pause and skip.
    /// It builds UI Toolkit elements for <see cref="SessionOverlay"/>'s lobby and results views.
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

        /// <summary>What the lobby's cup parts show, so the lobby rebuilds only when it changes.</summary>
        public string Signature => $"{_choice}/{_reverseGrid}/{PlayerPrefs.GetString(SaveKey, string.Empty).Length}";

        /// <summary>The host's lobby choices: single event or a cup, and for a cup, reverse grid; <paramref name="changed"/> runs after each change.</summary>
        public void AddLobbyChoice(VisualElement panel, Action changed)
        {
            string label = _choice == 0 ? "Event: single" : "Event: " + Cups.All[_choice - 1].Name;
            panel.Add(UiKit.Button(label, () =>
            {
                _choice = (_choice + 1) % (Cups.All.Count + 1);
                PlayerPrefs.SetInt(ChoiceKey, _choice);
                changed();
            }));

            if (!CupChosen)
            {
                return;
            }

            var cup = Cups.All[_choice - 1];
            panel.Add(UiKit.Hint($"{cup.Events.Count} events; the next starts 10 s after each finishes."));
            panel.Add(UiKit.Button(_reverseGrid ? "Reverse grid: on" : "Reverse grid: off", () =>
            {
                _reverseGrid = !_reverseGrid;
                PlayerPrefs.SetInt(ReverseGridKey, _reverseGrid ? 1 : 0);
                changed();
            }));
        }

        /// <summary>The host's start buttons for a cup: start a new one, or resume the saved one.</summary>
        public void AddStartButtons(VisualElement panel, bool canStart)
        {
            var host = (HostRace)_session.Race;
            var saved = LoadSave(host);
            if (saved != null)
            {
                panel.Add(UiKit.Enabled(UiKit.Button($"Resume {saved.Cup.Name} ({saved.Championship.EventsCompleted} of {saved.Cup.Events.Count} done)", () =>
                {
                    if (canStart)
                    {
                        Begin(saved);
                    }
                }), canStart));
            }

            var cup = Cups.All[_choice - 1];
            panel.Add(UiKit.Enabled(UiKit.Button(canStart ? "Start " + cup.Name : "Waiting for everyone to be ready", () =>
            {
                if (canStart)
                {
                    Begin(new CupRun(_session.HostSide, host, () => NetworkSession.NowMs, cup, _reverseGrid));
                }
            }, primary: true), canStart));
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

        /// <summary>What the standings show (not the countdown, which <see cref="UpdateCountdown"/> keeps current), so the results rebuild only when it changes.</summary>
        public string StandingsSignature(CupStandingsMessage table)
        {
            var text = new StringBuilder();
            text.Append(table.CupName).Append(table.EventsCompleted).Append('/').Append(table.TotalEvents).Append(table.IsComplete);
            text.Append(table.NextEventAtHostMs > 0).Append(Run != null && Run.Paused);
            foreach (var entry in table.Standings)
            {
                text.Append('|').Append(entry.Name).Append(entry.Points);
            }

            return text.ToString();
        }

        /// <summary>
        /// The cup table with the countdown to the next event, and on the host the pause, skip and
        /// leave buttons, or Back to lobby once the cup is won. <paramref name="countdown"/> is the
        /// label to keep current with <see cref="UpdateCountdown"/>; <paramref name="changed"/> runs
        /// after the host changes something.
        /// </summary>
        public VisualElement BuildStandings(CupStandingsMessage table, string me, Action changed, out Label countdown)
        {
            var panel = UiKit.Panel(460f);
            string heading = table.IsComplete ? $"{table.CupName}: final" : $"{table.CupName}: {table.EventsCompleted} of {table.TotalEvents}";
            panel.Add(UiKit.Heading(heading));
            for (int i = 0; i < table.Standings.Count; i++)
            {
                var entry = table.Standings[i];
                string you = string.Equals(entry.Name, me, StringComparison.OrdinalIgnoreCase) ? " (you)" : string.Empty;
                var row = UiKit.Row();
                var name = UiKit.Text($"{RaceFormat.Ordinal(i + 1)}  {entry.Name}{you}", 20, Color.white);
                name.style.flexGrow = 1f;
                row.Add(name);
                row.Add(UiKit.Text($"{entry.Points} pts", 20, Color.white));
                panel.Add(row);
            }

            panel.Add(UiKit.Space(12f));
            countdown = null;
            if (table.IsComplete)
            {
                panel.Add(UiKit.Text(table.Standings.Count > 0 ? $"{table.Standings[0].Name} wins the {table.CupName}!" : string.Empty, 20, UiKit.Accent, bold: true));
                if (_session.IsHost)
                {
                    panel.Add(UiKit.Button("Back to lobby", Finish, primary: true, sound: UiSound.Back));
                }

                return panel;
            }

            countdown = UiKit.Text(string.Empty, 20, Color.white);
            panel.Add(countdown);
            UpdateCountdown(table, countdown);
            if (_session.IsHost && Run != null)
            {
                var run = Run;
                panel.Add(UiKit.EvenRow(
                    UiKit.Button(run.Paused ? "Resume" : "Pause", () =>
                    {
                        run.SetPaused(!run.Paused);
                        changed();
                    }),
                    UiKit.Button("Next event", () => run.AdvanceNow()),
                    UiKit.Button("Leave cup", () =>
                    {
                        // Leaving keeps the save, so the cup can be resumed from the lobby.
                        Run = null;
                        ((HostRace)_session.Race).ReturnToLobby();
                    }, sound: UiSound.Back)));
            }

            return panel;
        }

        /// <summary>Sets the countdown line to the time left before the next event.</summary>
        public void UpdateCountdown(CupStandingsMessage table, Label countdown)
        {
            long nextAt = table.NextEventAtHostMs;
            if (nextAt > 0 && !_session.IsHost)
            {
                nextAt = _session.GuestSide.Clock.ToLocalTime(nextAt);
            }

            long msToGo = nextAt - NetworkSession.NowMs;
            string text = nextAt > 0 ? $"Next event in {Math.Max(0L, (msToGo + 999) / 1000)} s" : "Next event: waiting for the host";
            if (countdown.text != text)
            {
                countdown.text = text;
            }
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
        private void Finish()
        {
            Run = null;
            PlayerPrefs.DeleteKey(SaveKey);
            ((HostRace)_session.Race).ReturnToLobby();
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
