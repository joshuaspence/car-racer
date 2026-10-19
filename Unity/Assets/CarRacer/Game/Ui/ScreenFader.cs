using CarRacer.Core.Platform;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarRacer.Game.Ui
{
    /// <summary>
    /// Fades each new screen in from black (design doc §3, <see cref="ScreenFade"/>): a black layer
    /// over the screens (below the performance readout) that <see cref="Cover"/> makes opaque and
    /// that then lifts. It never takes touches, so the new screen can be used while it fades.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        private VisualElement _cover;
        private float _elapsed = ScreenFade.Total;

        /// <summary>Makes the fader, once; it outlives screens.</summary>
        public static void Install()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(ScreenFader));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ScreenFader>();
            var layer = UiRoot.Create(host.transform, "Screen fade", order: 90);
            _instance._cover = UiKit.Fill(new VisualElement { name = "cover" });
            _instance._cover.style.backgroundColor = Color.black;
            _instance._cover.style.display = DisplayStyle.None;
            layer.Full.Add(_instance._cover);
        }

        /// <summary>Hides the screen at once and starts the fade in; call it as a new screen is made.</summary>
        public static void Cover()
        {
            if (_instance == null)
            {
                return;
            }

            _instance._elapsed = 0f;
            _instance.Apply();
        }

        /// <summary>True while a fade is under way.</summary>
        public static bool Fading => _instance != null && !ScreenFade.Done(_instance._elapsed);

        private void Update()
        {
            if (ScreenFade.Done(_elapsed))
            {
                return;
            }

            _elapsed = ScreenFade.Advance(_elapsed, Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            float alpha = ScreenFade.Alpha(_elapsed);
            _cover.style.opacity = alpha;
            _cover.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
