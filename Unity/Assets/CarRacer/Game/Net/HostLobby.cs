using System;
using CarRacer.Core.Arena;
using CarRacer.Core.Bots;
using CarRacer.Core.Net;
using CarRacer.Core.Race;
using CarRacer.Core.Scoring;
using CarRacer.Core.Session;
using CarRacer.Core.Track;
using UnityEngine;

namespace CarRacer.Game.Net
{
    /// <summary>
    /// The host's side of the lobby (design doc §2.3, §10, §11), on a phone host and on a server
    /// alike: remembers the next event's settings between sessions, carries out the room leader's
    /// commands (start a single event or a cup, resume the saved cup, pause, skip, leave, back to
    /// the lobby), and runs the <see cref="CupRun"/> under way with its auto-save. The screens only
    /// send commands (<see cref="NetworkSession.Command"/>), so the same buttons work on the host's
    /// own phone and on a guest phone leading a server's room.
    /// </summary>
    public sealed class HostLobby : MonoBehaviour
    {
        public const string BotFillKey = "botFill";
        public const string BotDifficultyKey = "botDifficulty";
        public const string ModeKey = "mode";
        public const string LapsKey = "laps";
        public const string TrackKey = "track";
        public const string RulesKey = "eventRules";
        public const string CupKey = "cupChoice";
        public const string ReverseGridKey = "reverseGrid";
        public const string SaveKey = "cupSave";

        private NetworkSession _session;
        private string _savedText;
        private CupRun _saved;

        /// <summary>The cup in progress, or null.</summary>
        public CupRun Run { get; private set; }

        public void Initialise(NetworkSession session)
        {
            _session = session;
            var host = session.HostSide;
            host.ChangeSettings(Load());
            host.SettingsChanged += Save;
            host.LobbyCommandReceived += OnCommand;
            ReportSave();
        }

        private void OnDestroy()
        {
            if (_session != null && _session.HostSide != null)
            {
                _session.HostSide.SettingsChanged -= Save;
                _session.HostSide.LobbyCommandReceived -= OnCommand;
            }
        }

        private void Update()
        {
            if (_session.Ended || _session.Race == null)
            {
                return;
            }

            Run?.Tick();

            // A server whose players have all left (their cars now bots) has nobody to press Back to
            // lobby, and would refuse every new guest as mid-event; once the event's results are in,
            // it goes back by itself. A cup in progress is left, keeping its save to resume.
            if (_session.IsDedicated && _session.LeaderId == HostSession.NoPlayer && Race.Phase == RacePhase.Results)
            {
                Run = null;
                Race.ReturnToLobby();
            }
        }

        private HostRace Race => (HostRace)_session.Race;

        private void OnCommand(LobbyCommand command)
        {
            if (_session.Race == null)
            {
                return;
            }

            switch (command)
            {
                case LobbyCommand.Start:
                    if (Race.Phase != RacePhase.Lobby || !_session.HostSide.CanStart)
                    {
                        return;
                    }

                    var settings = _session.Settings;
                    if (settings.CupChosen)
                    {
                        Begin(new CupRun(_session.HostSide, Race, () => NetworkSession.NowMs, Cups.All[settings.Cup - 1], settings.ReverseGrid));
                    }
                    else
                    {
                        Race.Start(settings.Laps, settings.Mode, settings.Track);
                    }

                    break;
                case LobbyCommand.ResumeSavedCup:
                    var saved = LoadSave();
                    if (saved != null && Race.Phase == RacePhase.Lobby && _session.HostSide.CanStart)
                    {
                        Begin(saved);
                    }

                    break;
                case LobbyCommand.PauseCup:
                case LobbyCommand.UnpauseCup:
                    Run?.SetPaused(command == LobbyCommand.PauseCup);
                    break;
                case LobbyCommand.NextEvent:
                    Run?.AdvanceNow();
                    break;
                case LobbyCommand.LeaveCup:
                    // Leaving keeps the save, so the cup can be resumed from the lobby.
                    if (Run != null)
                    {
                        Run = null;
                        Race.ReturnToLobby();
                    }

                    break;
                case LobbyCommand.BackToLobby:
                    if (Run == null)
                    {
                        Race.ReturnToLobby();
                    }
                    else if (Run.IsComplete)
                    {
                        // The finished cup is done with: its save goes too.
                        Run = null;
                        PlayerPrefs.DeleteKey(SaveKey);
                        ReportSave();
                        Race.ReturnToLobby();
                    }

                    break;
            }
        }

        private void Begin(CupRun run)
        {
            if (!run.StartNextEvent())
            {
                return;
            }

            run.Saved += SaveCup;
            Run = run;
            _savedText = null;
        }

        private void SaveCup(byte[] save)
        {
            PlayerPrefs.SetString(SaveKey, Convert.ToBase64String(save));
            PlayerPrefs.Save();
            ReportSave();
        }

        /// <summary>Tells every phone which cup is saved, so the room leader can offer to resume it.</summary>
        private void ReportSave()
        {
            var saved = LoadSave();
            int cup = 0;
            for (int i = 0; saved != null && i < Cups.All.Count; i++)
            {
                if (Cups.All[i] == saved.Cup)
                {
                    cup = i + 1;
                }
            }

            _session.HostSide.SetSavedCup(cup, saved?.Championship.EventsCompleted ?? 0);
        }

        /// <summary>The saved cup, read again only when the save changes.</summary>
        private CupRun LoadSave()
        {
            string text = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (text == _savedText)
            {
                return _saved;
            }

            _savedText = text;
            _saved = null;
            if (text.Length > 0 && _session.Race != null)
            {
                try
                {
                    _saved = CupRun.Resume(Convert.FromBase64String(text), _session.HostSide, Race, () => NetworkSession.NowMs);
                }
                catch (FormatException)
                {
                }
            }

            return _saved;
        }

        private static LobbySettings Load()
        {
            var settings = LobbySettings.Default;
            settings.Mode = (GameMode)PlayerPrefs.GetInt(ModeKey, (int)GameMode.Race);
            settings.Laps = PlayerPrefs.GetInt(LapsKey, settings.Laps);
            settings.Track = PlayerPrefs.GetInt(TrackKey, 0);
            settings.Rules = EventRules.FromByte((byte)PlayerPrefs.GetInt(RulesKey, 0));
            settings.BotFill = PlayerPrefs.GetInt(BotFillKey, 0);
            settings.BotDifficulty = (BotDifficulty)PlayerPrefs.GetInt(BotDifficultyKey, (int)BotDifficulty.Normal);
            settings.Cup = PlayerPrefs.GetInt(CupKey, 0);
            settings.ReverseGrid = PlayerPrefs.GetInt(ReverseGridKey, 1) == 1;
            return settings.Clean();
        }

        private void Save()
        {
            var settings = _session.Settings;
            PlayerPrefs.SetInt(ModeKey, (int)settings.Mode);
            PlayerPrefs.SetInt(LapsKey, settings.Laps);
            PlayerPrefs.SetInt(TrackKey, TrackCatalog.Clamp(settings.Track));
            PlayerPrefs.SetInt(RulesKey, settings.Rules.ToByte());
            PlayerPrefs.SetInt(BotFillKey, settings.BotFill);
            PlayerPrefs.SetInt(BotDifficultyKey, (int)settings.BotDifficulty);
            PlayerPrefs.SetInt(CupKey, settings.Cup);
            PlayerPrefs.SetInt(ReverseGridKey, settings.ReverseGrid ? 1 : 0);
        }
    }
}
