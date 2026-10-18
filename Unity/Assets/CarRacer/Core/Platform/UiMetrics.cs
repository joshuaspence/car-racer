using System;

namespace CarRacer.Core.Platform
{
    /// <summary>Space to keep clear at each edge, in reference pixels from the top-left.</summary>
    public struct UiInsets
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public UiInsets(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    /// <summary>
    /// The UI's units (design doc §3): everything is laid out in reference pixels for a 720-pixel-tall
    /// screen and scaled to the device, so a button is the same physical share of the screen's
    /// height on every phone. These convert the device's pixels, and its safe area (clear of
    /// notches and rounded corners), into those units.
    /// </summary>
    public static class UiMetrics
    {
        public const float ReferenceHeight = 720f;

        /// <summary>Device pixels per reference pixel.</summary>
        public static float Scale(float screenHeight) => screenHeight > 0f ? screenHeight / ReferenceHeight : 1f;

        /// <summary>The screen's width in reference pixels.</summary>
        public static float Width(float screenWidth, float screenHeight) => screenWidth / Scale(screenHeight);

        /// <summary>
        /// How far the safe area sits in from each edge, in reference pixels. The safe area is in
        /// device pixels from the bottom-left (Unity's <c>Screen.safeArea</c>); the result is from
        /// the top-left, as the UI lays out. Never negative.
        /// </summary>
        public static UiInsets SafeInsets(float screenWidth, float screenHeight, float safeX, float safeY, float safeWidth, float safeHeight)
        {
            float scale = Scale(screenHeight);
            return new UiInsets(
                Math.Max(0f, safeX) / scale,
                Math.Max(0f, screenHeight - (safeY + safeHeight)) / scale,
                Math.Max(0f, screenWidth - (safeX + safeWidth)) / scale,
                Math.Max(0f, safeY) / scale);
        }
    }
}
