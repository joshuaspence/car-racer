using CarRacer.Core.Platform;
using Unity.Profiling;
using UnityEngine;

namespace CarRacer.Game.Platform
{
    /// <summary>
    /// The performance readout (design doc §14): frame rate, the slowest recent frame, frames that
    /// missed 30 fps, draw calls, memory and this phone's network traffic, in small text at the top
    /// of the screen over every screen. On by default in development builds; Settings turns it on
    /// or off (remembered). The draw-call and memory counters only report in development builds.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        public const string VisibleKey = "perfReadout";

        /// <summary>This phone's session traffic; <c>NetworkSession</c> counts into it.</summary>
        public static readonly TrafficCounter Traffic = new TrafficCounter();

        private static PerfOverlay _instance;
        private static bool? _visible;

        private readonly FrameStats _frames = new FrameStats();
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _memory;
        private GUIStyle _style;
        private Texture2D _backdrop;
        private string _text = string.Empty;
        private float _nextText;

        public static bool Visible
        {
            get => _visible ?? (bool)(_visible = PlayerPrefs.GetInt(VisibleKey, Debug.isDebugBuild ? 1 : 0) != 0);
            set
            {
                _visible = value;
                PlayerPrefs.SetInt(VisibleKey, value ? 1 : 0);
            }
        }

        /// <summary>For the Settings button.</summary>
        public static string Label => Visible ? "Perf stats: on" : "Perf stats: off";

        /// <summary>Makes the overlay, once; it outlives screens. Counting starts at once, so the readout is ready when shown.</summary>
        public static void Install()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(PerfOverlay));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PerfOverlay>();
        }

        private void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _memory.Dispose();
        }

        private void OnDestroy()
        {
            if (_backdrop != null)
            {
                Destroy(_backdrop);
            }
        }

        private void Update()
        {
            _frames.Add(Time.unscaledDeltaTime);
            Traffic.Roll(Time.realtimeSinceStartupAsDouble);
            if (Visible && Time.unscaledTime >= _nextText)
            {
                // A few times a second is readable; every frame would flicker and allocate.
                _nextText = Time.unscaledTime + 0.25f;
                int draws = _drawCalls.Valid ? (int)_drawCalls.LastValue : -1;
                int memoryMb = _memory.Valid ? (int)(_memory.LastValue / (1024 * 1024)) : -1;
                _text = PerfReadout.Format(_frames, draws, memoryMb, CarRacer.Game.Net.NetworkSession.Active ? Traffic : null);
            }
        }

        private void OnGUI()
        {
            if (!Visible)
            {
                return;
            }

            if (_style == null)
            {
                _backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _backdrop.Apply();
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, padding = new RectOffset(8, 8, 4, 4) };
                _style.normal.textColor = Color.white;
                _style.normal.background = _backdrop;
            }

            // Drawn last, over every other screen, just under the item slot at the top centre.
            GUI.depth = -100;
            float scale = Screen.height / CarRacer.Game.Menu.UiStyle.ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            GUI.Label(new Rect((width - 340f) / 2f, 76f, 340f, 64f), _text, _style);
        }
    }
}
