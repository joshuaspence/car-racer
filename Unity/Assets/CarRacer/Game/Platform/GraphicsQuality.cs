using CarRacer.Core.Platform;
using UnityEngine;
using QualityLevel = CarRacer.Core.Platform.QualityLevel;

namespace CarRacer.Game.Platform
{
    /// <summary>
    /// Applies the quality level (design doc §14): picked automatically from the GPU and memory
    /// (<see cref="QualityPresets"/>) unless the player has chosen one in the main menu. It sets the
    /// rendering resolution as a share of the screen's and the MSAA level.
    /// </summary>
    public static class GraphicsQuality
    {
        public const string ChoiceKey = "quality";

        /// <summary>The player's choice: -1 for automatic, or a <see cref="QualityLevel"/>.</summary>
        public static int Choice
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ChoiceKey, -1), -1, (int)QualityLevel.High);
            private set => PlayerPrefs.SetInt(ChoiceKey, value);
        }

        /// <summary>The level automatic picks on this phone.</summary>
        public static QualityLevel Automatic => QualityPresets.Pick(SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize);

        public static QualityLevel Current => Choice < 0 ? Automatic : (QualityLevel)Choice;

        /// <summary>For the menu button, e.g. "Graphics: auto (Low)".</summary>
        public static string Label => Choice < 0 ? $"Graphics: auto ({Automatic})" : $"Graphics: {Current}";

        /// <summary>Steps through auto, Low, Medium and High, and applies the new level.</summary>
        public static void Cycle()
        {
            Choice = Choice >= (int)QualityLevel.High ? -1 : Choice + 1;
            Apply();
        }

        public static void Apply()
        {
            var values = QualityPresets.For(Current);
            QualitySettings.antiAliasing = values.Msaa;

            // In the editor the game view sets the size; on a phone this lowers the rendering resolution.
            if (!Application.isEditor)
            {
                var display = Display.main;
                int width = Mathf.RoundToInt(display.systemWidth * values.RenderScale);
                int height = Mathf.RoundToInt(display.systemHeight * values.RenderScale);
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            }
        }
    }
}
