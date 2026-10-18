using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Menu
{
    /// <summary>
    /// One look for every menu and overlay: a dark panel style, big touch-sized buttons (at least
    /// 60 reference pixels tall, about 9 mm on a phone), and a yellow accent. Layout is in reference
    /// pixels for a 720-pixel-tall screen and scaled to the device. IMGUI styles can only be built
    /// inside OnGUI, so <see cref="Begin"/> creates them the first time.
    /// </summary>
    public static class UiStyle
    {
        public const float ReferenceHeight = 720f;
        public const float ButtonHeight = 64f;

        public static readonly Color Background = new Color(0.10f, 0.12f, 0.18f);
        public static readonly Color PanelColour = new Color(0.06f, 0.07f, 0.11f, 0.88f);
        public static readonly Color Accent = new Color(0.98f, 0.78f, 0.15f);
        public static readonly Color Muted = new Color(0.70f, 0.74f, 0.82f);

        private static Texture2D _panel;
        private static Texture2D _button;
        private static Texture2D _buttonPressed;
        private static Texture2D _primary;
        private static Texture2D _primaryPressed;
        private static Texture2D _field;

        /// <summary>The large-text setting the styles were last built for.</summary>
        private static bool _builtLargeText;

        public static GUIStyle Title { get; private set; }
        public static GUIStyle Heading { get; private set; }
        public static GUIStyle HeadingCentred { get; private set; }
        public static GUIStyle Body { get; private set; }
        public static GUIStyle Hint { get; private set; }
        public static GUIStyle HintCentred { get; private set; }
        public static GUIStyle Button { get; private set; }
        public static GUIStyle PrimaryButton { get; private set; }
        public static GUIStyle Field { get; private set; }
        public static GUIStyle Panel { get; private set; }

        public static float Scale => Screen.height / ReferenceHeight;

        /// <summary>Screen width in reference pixels.</summary>
        public static float Width => Screen.width / Scale;

        /// <summary>The safe area (clear of notches and rounded corners) in reference pixels, IMGUI orientation.</summary>
        public static Rect SafeArea
        {
            get
            {
                var safe = Screen.safeArea;
                float s = Scale;
                return new Rect(safe.x / s, (Screen.height - safe.yMax) / s, safe.width / s, safe.height / s);
            }
        }

        /// <summary>Call at the start of OnGUI: sets the scale and makes sure the styles exist.</summary>
        public static void Begin()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            if (Title != null && _panel != null && _builtLargeText == Accessibility.LargeText)
            {
                return;
            }

            // Rebuilt when large text is switched (design doc §11), so it applies at once.
            _builtLargeText = Accessibility.LargeText;
            if (_panel == null)
            {
                _panel = Solid(PanelColour);
                _button = Solid(new Color(0.22f, 0.26f, 0.36f));
                _buttonPressed = Solid(new Color(0.32f, 0.38f, 0.52f));
                _primary = Solid(Accent);
                _primaryPressed = Solid(new Color(1f, 0.88f, 0.45f));
                _field = Solid(new Color(0.16f, 0.18f, 0.25f));
            }

            Title = new GUIStyle(GUI.skin.label) { fontSize = Accessibility.Text(64), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Title.normal.textColor = Accent;
            Heading = new GUIStyle(GUI.skin.label) { fontSize = Accessibility.Text(34), fontStyle = FontStyle.Bold };
            Heading.normal.textColor = Color.white;
            HeadingCentred = new GUIStyle(Heading) { alignment = TextAnchor.MiddleCenter };
            Body = new GUIStyle(GUI.skin.label) { fontSize = Accessibility.Text(26), wordWrap = true };
            Body.normal.textColor = Color.white;
            Hint = new GUIStyle(Body) { fontSize = Accessibility.Text(21) };
            Hint.normal.textColor = Muted;
            HintCentred = new GUIStyle(Hint) { alignment = TextAnchor.MiddleCenter };

            Button = new GUIStyle(GUI.skin.button) { fontSize = Accessibility.Text(28), fontStyle = FontStyle.Bold, fixedHeight = ButtonHeight, margin = new RectOffset(0, 0, 8, 8) };
            SetBackgrounds(Button, _button, _buttonPressed, Color.white);
            PrimaryButton = new GUIStyle(Button);
            SetBackgrounds(PrimaryButton, _primary, _primaryPressed, new Color(0.08f, 0.08f, 0.1f));

            Field = new GUIStyle(GUI.skin.textField) { fontSize = Accessibility.Text(28), fixedHeight = ButtonHeight, padding = new RectOffset(16, 16, 12, 12), alignment = TextAnchor.MiddleLeft };
            Field.normal.background = Field.focused.background = Field.hover.background = Field.active.background = _field;
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Color.white;

            Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 20, 20) };
            Panel.normal.background = _panel;
        }

        /// <summary>A panel of <paramref name="width"/> reference pixels, centred horizontally in the safe area.</summary>
        public static Rect CentredPanel(float width, float top, float height)
        {
            var safe = SafeArea;
            float w = Mathf.Min(width, safe.width - 32f);
            return new Rect(safe.x + (safe.width - w) / 2f, top, w, Mathf.Min(height, ReferenceHeight - top - 16f));
        }

        private static void SetBackgrounds(GUIStyle style, Texture2D normal, Texture2D pressed, Color text)
        {
            style.normal.background = style.hover.background = style.focused.background = normal;
            style.active.background = style.onActive.background = pressed;
            style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = text;
        }

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }
    }
}
