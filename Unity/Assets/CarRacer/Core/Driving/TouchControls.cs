using System;
using System.Collections.Generic;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Driving
{
    public enum TouchStage
    {
        Began,
        Held,
        Ended,
    }

    /// <summary>One finger on the screen, in pixels from the bottom-left corner (Unity's touch coordinates).</summary>
    public struct TouchPoint
    {
        public int Id;
        public Vec2 Position;
        public TouchStage Stage;

        public TouchPoint(int id, float x, float y, TouchStage stage)
        {
            Id = id;
            Position = new Vec2(x, y);
            Stage = stage;
        }
    }

    /// <summary>
    /// Where the on-screen controls sit (design doc §5): the left half steers, and round Drift,
    /// Brake, Item and (with auto-accelerate off) Gas buttons sit on the right, inside the safe area.
    /// In a race a Look back button sits in the top-left corner, below the HUD's text.
    /// Sizes are in physical inches so they suit any screen density. The left-handed layout is the
    /// mirror image (buttons on the left, the right half steers), except Look back, which stays
    /// top-left (clear of the HUD's position, lap and mini-map on the right, and on the buttons'
    /// side). Large controls make every button bigger (accessibility, design doc §5).
    /// </summary>
    public sealed class TouchControlLayout
    {
        public const float ButtonRadiusInches = 0.42f;
        public const float SteerRangeInches = 0.6f;
        public const float MarginInches = 0.25f;

        /// <summary>Touches this far outside a button (as a fraction of its radius) still press it.</summary>
        public const float HitSlack = 1.25f;

        /// <summary>Room left above the Look back button for the HUD's text.</summary>
        public const float TopTextInches = 0.45f;

        /// <summary>Assumed screen height in inches when the device doesn't report its DPI.</summary>
        public const float FallbackScreenHeightInches = 2.8f;

        /// <summary>How much bigger the buttons are with large controls.</summary>
        public const float LargeButtonScale = 1.25f;

        /// <param name="leftHanded">Mirror the layout: buttons on the left, the right half steers.</param>
        /// <param name="buttonScale">Button size relative to the standard (see <see cref="LargeButtonScale"/>).</param>
        public TouchControlLayout(float screenWidth, float screenHeight, float safeLeft, float safeBottom, float safeRight, float safeTop, float dpi, bool leftHanded = false, float buttonScale = 1f)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            LeftHanded = leftHanded;
            ButtonScale = buttonScale;
            float pixelsPerInch = dpi > 0f ? dpi : screenHeight / FallbackScreenHeightInches;
            ButtonRadius = ButtonRadiusInches * buttonScale * pixelsPerInch;
            SteerRange = SteerRangeInches * pixelsPerInch;
            float margin = MarginInches * pixelsPerInch;

            SteerZoneRight = screenWidth / 2f;
            DriftCentre = new Vec2(safeRight - margin - ButtonRadius, safeBottom + margin + ButtonRadius);
            BrakeCentre = new Vec2(DriftCentre.X - 2.4f * ButtonRadius, DriftCentre.Y + 0.4f * ButtonRadius);
            GasCentre = new Vec2(DriftCentre.X, Math.Min(safeTop - margin - ButtonRadius, DriftCentre.Y + 2.6f * ButtonRadius));
            ItemCentre = new Vec2(BrakeCentre.X, Math.Min(safeTop - margin - ButtonRadius, BrakeCentre.Y + 2.4f * ButtonRadius));
            LookBackCentre = new Vec2(safeLeft + margin + ButtonRadius, safeTop - TopTextInches * pixelsPerInch - ButtonRadius);
            if (leftHanded)
            {
                // Mirrored about the safe area's middle, so the buttons keep their margin from a notch.
                float mirror = safeLeft + safeRight;
                DriftCentre = Mirror(DriftCentre, mirror);
                BrakeCentre = Mirror(BrakeCentre, mirror);
                GasCentre = Mirror(GasCentre, mirror);
                ItemCentre = Mirror(ItemCentre, mirror);
            }
        }

        public float ScreenWidth { get; }
        public bool LeftHanded { get; }
        public float ButtonScale { get; }
        public float ScreenHeight { get; }
        public float ButtonRadius { get; }

        /// <summary>How far, in pixels, the steering thumb drags for full lock.</summary>
        public float SteerRange { get; }

        /// <summary>The line between the steering half and the buttons' half: touches starting left of it steer (right of it, left-handed).</summary>
        public float SteerZoneRight { get; }

        /// <summary>True if a touch starting at <paramref name="x"/> is on the steering half.</summary>
        public bool InSteerZone(float x) => LeftHanded ? x > SteerZoneRight : x < SteerZoneRight;

        public Vec2 DriftCentre { get; }
        public Vec2 BrakeCentre { get; }
        public Vec2 GasCentre { get; }
        public Vec2 ItemCentre { get; }
        public Vec2 LookBackCentre { get; }

        public bool HitsButton(Vec2 point, Vec2 centre)
        {
            float dx = point.X - centre.X;
            float dy = point.Y - centre.Y;
            float reach = ButtonRadius * HitSlack;
            return dx * dx + dy * dy <= reach * reach;
        }

        private static Vec2 Mirror(Vec2 point, float mirror) => new Vec2(mirror - point.X, point.Y);
    }

    /// <summary>
    /// Analog steering from a thumb drag (design doc §5). The centre is wherever the thumb lands;
    /// dragging past full lock drags the centre along, so moving back always straightens up promptly.
    /// </summary>
    public sealed class TouchSteering
    {
        /// <summary>Fraction of full lock ignored around the centre so a resting thumb drives straight.</summary>
        public const float DeadZone = 0.06f;

        private float _origin;
        private float _offset;

        public bool Active { get; private set; }

        public float Steer
        {
            get
            {
                float magnitude = Math.Abs(_offset);
                if (magnitude <= DeadZone)
                {
                    return 0f;
                }

                return Math.Sign(_offset) * Math.Min(1f, (magnitude - DeadZone) / (1f - DeadZone));
            }
        }

        public void Begin(float x)
        {
            _origin = x;
            _offset = 0f;
            Active = true;
        }

        public void Move(float x, float range)
        {
            float offset = x - _origin;
            if (offset > range)
            {
                _origin = x - range;
                offset = range;
            }
            else if (offset < -range)
            {
                _origin = x + range;
                offset = -range;
            }

            _offset = range > 0f ? offset / range : 0f;
        }

        public void End()
        {
            _offset = 0f;
            Active = false;
        }
    }

    /// <summary>
    /// Turns raw touches into <see cref="DriverControls"/>. A steering thumb keeps steering until it
    /// lifts, even if it strays right. Any other finger presses whichever button it is over, so a
    /// thumb can rock from Brake to Gas or slide onto a button it just missed; over empty screen it
    /// keeps its last button, so slipping off doesn't let go. Item is the exception: firing an item
    /// by accident is costly, so only a finger that lands on it uses the item, once per tap. Look
    /// back is like Item: only a finger that lands on it looks back, until that finger lifts.
    /// </summary>
    public sealed class TouchControls
    {
        private enum Role
        {
            None,
            Steer,
            Drift,
            Brake,
            Gas,
            Item,
            LookBack,
        }

        private readonly Dictionary<int, Role> _roles = new Dictionary<int, Role>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly TouchSteering _steering = new TouchSteering();
        private int _steeringFinger = -1;

        public bool DriftHeld { get; private set; }
        public bool BrakeHeld { get; private set; }
        public bool GasHeld { get; private set; }
        public bool ItemHeld { get; private set; }
        public bool LookBackHeld { get; private set; }

        /// <param name="showGas">The Gas button is shown (auto-accelerate off).</param>
        /// <param name="showItem">The Item button is shown (in a race); a hidden button takes no fingers.</param>
        /// <param name="showLookBack">The Look back button is shown (on a track, with the chase camera).</param>
        public DriverControls Update(IReadOnlyList<TouchPoint> touches, TouchControlLayout layout, bool showGas, bool showItem = false, bool showLookBack = false)
        {
            _seen.Clear();
            bool itemTapped = false;
            foreach (var touch in touches)
            {
                _seen.Add(touch.Id);
                if (touch.Stage == TouchStage.Ended)
                {
                    Release(touch.Id);
                    continue;
                }

                // A finger first seen mid-press (its Began frame was missed) is treated as just landing.
                if (!_roles.TryGetValue(touch.Id, out var role))
                {
                    role = Assign(touch, layout, showGas, showItem, showLookBack);
                    itemTapped |= role == Role.Item;
                }
                else if (role != Role.Steer && role != Role.Item && role != Role.LookBack)
                {
                    var under = ButtonAt(touch.Position, layout, showGas, includeItem: false, includeLookBack: false);
                    if (under != Role.None)
                    {
                        role = under;
                    }
                }

                _roles[touch.Id] = role;

                if (role == Role.Steer)
                {
                    _steering.Move(touch.Position.X, layout.SteerRange);
                }
            }

            // Fingers that vanished without an Ended (app paused, touch cancelled) are released too.
            _gone.Clear();
            foreach (int id in _roles.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (int id in _gone)
            {
                Release(id);
            }

            DriftHeld = BrakeHeld = GasHeld = ItemHeld = LookBackHeld = false;
            foreach (var role in _roles.Values)
            {
                DriftHeld |= role == Role.Drift;
                BrakeHeld |= role == Role.Brake;
                GasHeld |= role == Role.Gas;
                ItemHeld |= role == Role.Item;
                LookBackHeld |= role == Role.LookBack;
            }

            return new DriverControls
            {
                Steer = _steering.Steer,
                Accelerate = GasHeld ? 1f : 0f,
                Brake = BrakeHeld ? 1f : 0f,
                Drift = DriftHeld,
                UseItem = itemTapped,
                LookBack = LookBackHeld,
            };
        }

        public bool Steering => _steering.Active;

        private Role Assign(TouchPoint touch, TouchControlLayout layout, bool showGas, bool showItem, bool showLookBack)
        {
            var button = ButtonAt(touch.Position, layout, showGas, includeItem: showItem, includeLookBack: showLookBack);
            if (button != Role.None)
            {
                return button;
            }

            if (layout.InSteerZone(touch.Position.X) && _steeringFinger < 0)
            {
                _steeringFinger = touch.Id;
                _steering.Begin(touch.Position.X);
                return Role.Steer;
            }

            return Role.None;
        }

        /// <summary>The nearest button within reach of <paramref name="point"/>, since some buttons' reaches overlap.</summary>
        private static Role ButtonAt(Vec2 point, TouchControlLayout layout, bool showGas, bool includeItem, bool includeLookBack)
        {
            var best = Role.None;
            float bestDistance = float.MaxValue;
            Consider(point, layout, layout.DriftCentre, Role.Drift, ref best, ref bestDistance);
            Consider(point, layout, layout.BrakeCentre, Role.Brake, ref best, ref bestDistance);
            if (showGas)
            {
                Consider(point, layout, layout.GasCentre, Role.Gas, ref best, ref bestDistance);
            }

            // A finger sliding over Item must not stop a nearer pedal from winning, nor take Item, so
            // Item only competes for a finger that has just landed.
            if (includeItem)
            {
                Consider(point, layout, layout.ItemCentre, Role.Item, ref best, ref bestDistance);
            }

            if (includeLookBack)
            {
                Consider(point, layout, layout.LookBackCentre, Role.LookBack, ref best, ref bestDistance);
            }

            return best;
        }

        private static void Consider(Vec2 point, TouchControlLayout layout, Vec2 centre, Role role, ref Role best, ref float bestDistance)
        {
            float dx = point.X - centre.X;
            float dy = point.Y - centre.Y;
            float distance = dx * dx + dy * dy;
            if (distance < bestDistance && layout.HitsButton(point, centre))
            {
                best = role;
                bestDistance = distance;
            }
        }

        private void Release(int id)
        {
            _roles.Remove(id);
            if (id == _steeringFinger)
            {
                _steeringFinger = -1;
                _steering.End();
            }
        }
    }
}
