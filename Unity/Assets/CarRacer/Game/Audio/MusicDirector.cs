using CarRacer.Core.Arena;
using CarRacer.Core.Audio;
using CarRacer.Core.Race;
using CarRacer.Game.DriveTest;
using UnityEngine;

namespace CarRacer.Game.Audio
{
    /// <summary>Picks the tune for the driving screen (<see cref="MusicChoice"/>): the menu tune in a lobby, the track's or the arena's in an event or practice.</summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        private DriveTestArena _arena;

        public void Initialise(DriveTestArena arena) => _arena = arena;

        private void Update()
        {
            if (_arena == null)
            {
                return;
            }

            // Practice has no session: it is always under way, on the track.
            var race = _arena.Session != null ? _arena.Session.Race : null;
            bool inEvent = _arena.Session == null || (race != null && race.Phase != RacePhase.Lobby);
            var mode = race != null ? race.Mode : GameMode.Race;
            MusicPlayer.Play(MusicChoice.For(inEvent, mode, _arena.TrackIndex));
        }
    }
}
