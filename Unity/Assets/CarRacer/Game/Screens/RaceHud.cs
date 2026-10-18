using System.Collections.Generic;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Track;
using CarRacer.Game.Bots;
using CarRacer.Game.Net;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Screens
{
    /// <summary>
    /// The driving HUD (design doc §6): a drift charge meter, the "Wrong way!" warning, a mini-map
    /// with a dot for every car, arrows at the screen's edge for cars behind or beside you, and a
    /// warning when a Missile is chasing you. Works alone and in a session. Temporary IMGUI until
    /// the real UI.
    /// </summary>
    public sealed class RaceHud : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const float MapSize = 160f;
        private const float MeterWidth = 300f;
        private const float ArrowMin = 26f;
        private const float ArrowMax = 64f;
        private const float ArrowInset = 12f;

        private static readonly Color MissileColour = new Color(1f, 0.2f, 0.15f);

        private static readonly Color[] TierColours =
        {
            new Color(0.6f, 0.6f, 0.6f),  // charging, no boost yet
            new Color(0.35f, 0.7f, 1f),   // tier 1
            new Color(1f, 0.6f, 0.15f),   // tier 2
            new Color(0.9f, 0.35f, 1f),   // tier 3
        };

        private TrackScreen _screen;
        private TrackProgress _progress;
        private MiniMap _map;
        private TrackLayout _mapTrack;
        private NetworkedCars _cars;
        private BotCars _bots;
        private GUIStyle _warning;
        private GUIStyle _meterLabel;
        private Texture2D _arrow;
        private readonly List<Vec3> _rivalPositions = new List<Vec3>();
        private readonly List<Color> _rivalColours = new List<Color>();
        private readonly List<EdgeIndicator> _arrows = new List<EdgeIndicator>();

        /// <summary>True while this phone's car has been facing backwards for a while.</summary>
        public bool IsWrongWay => _progress != null && _progress.IsWrongWay;

        public void Initialise(TrackScreen screen)
        {
            _screen = screen;
            FollowTrack();
        }

        /// <summary>Rebuilds the wrong-way detector and mini-map when racing moves to another track.</summary>
        private void FollowTrack()
        {
            if (_mapTrack == _screen.Track)
            {
                return;
            }

            _mapTrack = _screen.Track;
            _progress = new TrackProgress(_mapTrack.Spline);
            _map = new MiniMap(_mapTrack.Spline);
        }

        private void OnDestroy()
        {
            if (_arrow != null)
            {
                Destroy(_arrow);
            }
        }

        private void Update()
        {
            FollowTrack();
            var car = _screen.Car;
            if (car == null)
            {
                return;
            }

            _progress.Update(car.BodyPosition.ToCore(), car.State.Heading, car.ForwardSpeed, Time.deltaTime);
        }

        private void OnGUI()
        {
            var car = _screen.Car;
            if (car == null)
            {
                return;
            }

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            // The track's map and wrong-way warning mean nothing in an arena, which has the overhead camera instead.
            bool inArena = _screen.CameraRig.ArenaCentre.HasValue;
            if (!inArena)
            {
                // Opposite Look back, which moves top-right with left-handed controls (design doc §5).
                bool left = Accessibility.LeftHanded;
                DrawMiniMap(new Rect(left ? 24f : width - MapSize - 24f, left ? 184f + SessionOverlay.LeftCornerDrop : 184f, MapSize, MapSize));
            }

            DrawDriftMeter(width);

            if (!inArena)
            {
                // The overhead camera shows every car in an arena; a chase camera needs the arrows.
                DrawRivalArrows(width);
                DrawMissileWarning(width);
            }

            if (_progress.IsWrongWay && !inArena)
            {
                Warn(new Rect(0f, 340f, width, 80f), "Wrong way!", new Color(1f, 0.25f, 0.2f));
            }
        }

        private void Warn(Rect area, string text, Color colour)
        {
            _warning = _warning ?? new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var previous = GUI.contentColor;
            GUI.contentColor = colour;
            GUI.Label(area, text, _warning);
            GUI.contentColor = previous;
        }

        /// <summary>A coloured arrow at the screen's edge for each nearby car the camera can't show, growing as it closes in.</summary>
        private void DrawRivalArrows(float width)
        {
            _rivalPositions.Clear();
            _rivalColours.Clear();
            _cars = _cars != null ? _cars : GetComponent<NetworkedCars>();
            if (_cars != null)
            {
                foreach (var remote in _cars.RemoteCars)
                {
                    if (remote.HasState)
                    {
                        _rivalPositions.Add(remote.transform.position.ToCore());
                        _rivalColours.Add(remote.Colour);
                    }
                }
            }

            _bots = _bots != null ? _bots : GetComponent<BotCars>();
            if (_bots != null)
            {
                foreach (var (position, colour) in _bots.Dots)
                {
                    _rivalPositions.Add(position.ToCore());
                    _rivalColours.Add(colour);
                }
            }

            var car = _screen.Car;
            HudIndicators.Rivals(car.BodyPosition.ToCore(), ViewHeading(), _rivalPositions, _arrows);
            foreach (var arrow in _arrows)
            {
                DrawArrow(arrow.Bearing, Mathf.Lerp(ArrowMin, ArrowMax, arrow.Closeness), _rivalColours[arrow.Index], width);
            }
        }

        /// <summary>A flashing "Missile!" and a red arrow towards it while a Missile is chasing this phone's car.</summary>
        private void DrawMissileWarning(float width)
        {
            var session = _screen.Session;
            if (session == null || session.Items == null || !session.TryGetLocalPlayerId(out byte me))
            {
                return;
            }

            var car = _screen.Car;
            var position = car.BodyPosition.ToCore();
            if (!HudIndicators.TryIncomingMissile(session.Items.Projectiles, me, position, out var missile, out _))
            {
                return;
            }

            // Reduced motion (design doc §6) keeps the warning up instead of flashing it.
            bool lit = Accessibility.ReducedMotion || Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.6f;
            DrawArrow(HudIndicators.Bearing(position, ViewHeading(), missile.Position), ArrowMax, MissileColour, width);
            if (lit)
            {
                Warn(new Rect(0f, 140f, width, 80f), "Missile!", MissileColour);
            }
        }

        /// <summary>The way the camera faces: the car's heading, or the opposite during the rear-view glance, so arrows match the screen.</summary>
        private float ViewHeading() => _screen.Car.State.Heading + (_screen.CameraRig.LookingBack ? Mathf.PI : 0f);

        private void DrawArrow(float bearing, float size, Color colour, float width)
        {
            _arrow = _arrow != null ? _arrow : BuildArrow();
            Vec2 at = HudIndicators.EdgePoint(bearing, width, ReferenceHeight, size / 2f + ArrowInset);
            var pivot = new Vector3(at.X, at.Y, 0f);

            // Turn the up-pointing arrow to face the car, about its own centre, before the HUD's scaling.
            var previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix
                * Matrix4x4.Translate(pivot)
                * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, bearing * Mathf.Rad2Deg))
                * Matrix4x4.Translate(-pivot);
            var previousColour = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(at.X - size / 2f, at.Y - size / 2f, size, size), _arrow);
            GUI.color = previousColour;
            GUI.matrix = previousMatrix;
        }

        /// <summary>A white triangle pointing up, tinted per arrow.</summary>
        private static Texture2D BuildArrow()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float centre = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                // Row 0 is the bottom of the texture: the arrow's base, full width; the tip is at the top.
                float halfWidth = (size - 1 - y) / 2f;
                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - centre) <= halfWidth;
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawDriftMeter(float width)
        {
            var car = _screen.Car;
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

            // The tier in words too, so the colour isn't the only way to tell (design doc §6).
            _meterLabel = _meterLabel ?? new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
            _meterLabel.fontSize = Accessibility.Text(20);
            GUI.Label(new Rect(bar.x, bar.y - 40f, bar.width, 34f), tier > 0 ? $"BOOST {tier}" : "DRIFT", _meterLabel);

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
            Vec2 me = _map.ToMap(_screen.Car.BodyPosition.ToCore());
            Dot(area, me, 14f, Color.white);
            Dot(area, me, 10f, _screen.CarColour);
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
