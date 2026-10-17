using CarRacer.Core.Session;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Car paint for each roster colour index (<see cref="LobbyRoster.ColourCount"/> of them),
    /// chosen to stay distinct for colour-blind players (Okabe–Ito based, design doc §11).
    /// </summary>
    public static class CarColours
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.75f, 0.10f), // yellow
            new Color(0.00f, 0.45f, 0.70f), // blue
            new Color(0.84f, 0.37f, 0.00f), // vermilion
            new Color(0.00f, 0.62f, 0.45f), // bluish green
            new Color(0.80f, 0.47f, 0.65f), // reddish purple
            new Color(0.34f, 0.71f, 0.91f), // sky blue
            new Color(0.95f, 0.95f, 0.95f), // white
            new Color(0.20f, 0.20f, 0.22f), // black
        };

        public static Color ForIndex(int colourIndex) => Palette[((colourIndex % Palette.Length) + Palette.Length) % Palette.Length];
    }
}
