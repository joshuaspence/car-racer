using CarRacer.Core.Platform;
using UnityEngine;

namespace CarRacer.Game.Menu
{
    /// <summary>Draws a <see cref="QrCode"/> into a texture, one pixel per module with the 4-module white border readers need.</summary>
    public static class QrTexture
    {
        private const int QuietZone = 4;

        /// <summary>A new texture (the caller destroys it) showing <paramref name="text"/> as a QR code.</summary>
        public static Texture2D Build(string text)
        {
            var code = QrCode.Encode(text);
            int size = code.Size + 2 * QuietZone;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Join QR code",
            };
            var pixels = new Color32[size * size];
            var dark = new Color32(0, 0, 0, 255);
            var light = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture rows run bottom-up; QR rows run top-down.
                    bool isDark = code.IsDark(x - QuietZone, size - 1 - y - QuietZone);
                    pixels[y * size + x] = isDark ? dark : light;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
