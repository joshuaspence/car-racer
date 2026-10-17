using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Track;
using CarRacer.Game.Bots;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.DriveTest
{
    /// <summary>
    /// The driving HUD (design doc §6): a drift charge meter, the "Wrong way!" warning and a
    /// mini-map with a dot for every car. Works alone and in a session. Temporary IMGUI until the
    /// real UI later in M1.
    /// </summary>
    public sealed class RaceHud : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const float MapSize = 160f;
        private const float MeterWidth = 300f;

        private static readonly Color[] TierColours =
        {
            new Color(0.6f, 0.6f, 0.6f),  // charging, no boost yet
            new Color(0.35f, 0.7f, 1f),   // tier 1
            new Color(1f, 0.6f, 0.15f),   // tier 2
            new Color(0.9f, 0.35f, 1f),   // tier 3
        };

        private DriveTestArena _arena;
        private TrackProgress _progress;
        private MiniMap _map;
        private NetworkedCars _cars;
        private BotCars _bots;
        private GUIStyle _warning;

        /// <summary>True while this phone's car has been facing backwards for a while.</summary>
        public bool IsWrongWay => _progress != null && _progress.IsWrongWay;

        public void Initialise(DriveTestArena arena)
        {
            _arena = arena;
            _progress = new TrackProgress(arena.Track.Spline);
            _map = new MiniMap(arena.Track.Spline);
        }

        private void Update()
        {
            var car = _arena.Car;
            if (car == null)
            {
                return;
            }

            _progress.Update(car.BodyPosition.ToCore(), car.State.Heading, car.ForwardSpeed, Time.deltaTime);
        }

        private void OnGUI()
        {
            var car = _arena.Car;
            if (car == null)
            {
                return;
            }

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            DrawMiniMap(new Rect(width - MapSize - 24f, 184f, MapSize, MapSize));
            DrawDriftMeter(width);

            if (_progress.IsWrongWay)
            {
                _warning = _warning ?? new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                var previous = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.25f, 0.2f);
                GUI.Label(new Rect(0f, 340f, width, 80f), "Wrong way!", _warning);
                GUI.contentColor = previous;
            }
        }

        private void DrawDriftMeter(float width)
        {
            var car = _arena.Car;
            var state = car.State;
            if (!state.Drifting)
            {
                return;
            }

            var tuning = car.Tuning;
            float charge = state.DriftCharge(tuning);
            int tier = state.DriftTier(tuning);
            var bar = new Rect((width - MeterWidth) / 2f, ReferenceHeight - 64f, MeterWidth, 18f);

            Fill(bar, new Color(0f, 0f, 0f, 0.5f));
            Fill(new Rect(bar.x, bar.y, bar.width * charge, bar.height), TierColours[Mathf.Clamp(tier, 0, TierColours.Length - 1)]);

            // A tick at each tier's threshold.
            float full = tuning.DriftTierSeconds[tuning.DriftTierSeconds.Length - 1];
            foreach (float seconds in tuning.DriftTierSeconds)
            {
                float x = bar.x + bar.width * (seconds / full);
                Fill(new Rect(x - 1.5f, bar.y - 4f, 3f, bar.height + 8f), Color.white);
            }
        }

        private void DrawMiniMap(Rect area)
        {
            Fill(area, new Color(0f, 0f, 0f, 0.35f));
            var outline = new Color(1f, 1f, 1f, 0.8f);
            foreach (var point in _map.Outline)
            {
                Dot(area, point, 3f, outline);
            }

            _cars = _cars != null ? _cars : GetComponent<NetworkedCars>();
            if (_cars != null)
            {
                foreach (var remote in _cars.RemoteCars)
                {
                    if (remote.HasState)
                    {
                        Dot(area, _map.ToMap(remote.transform.position.ToCore()), 10f, remote.Colour);
                    }
                }
            }

            _bots = _bots != null ? _bots : GetComponent<BotCars>();
            if (_bots != null)
            {
                foreach (var (position, colour) in _bots.Dots)
                {
                    Dot(area, _map.ToMap(position.ToCore()), 10f, colour);
                }
            }

            // This phone's car on top, with a white ring so it stands out.
            Vec2 me = _map.ToMap(_arena.Car.BodyPosition.ToCore());
            Dot(area, me, 14f, Color.white);
            Dot(area, me, 10f, _arena.CarColour);
        }

        /// <summary>Map points run 0..1 from the bottom-left; IMGUI runs from the top-left.</summary>
        private static void Dot(Rect area, Vec2 point, float size, Color colour)
        {
            float x = area.x + point.X * area.width;
            float y = area.y + (1f - point.Y) * area.height;
            Fill(new Rect(x - size / 2f, y - size / 2f, size, size), colour);
        }

        private static void Fill(Rect rect, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
