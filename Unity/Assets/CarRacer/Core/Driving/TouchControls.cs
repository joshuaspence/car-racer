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
    /// Brake and (with auto-accelerate off) Gas buttons sit on the right, inside the safe area.
    /// Sizes are in physical inches so they suit any screen density.
    /// </summary>
    public sealed class TouchControlLayout
    {
        public const float ButtonRadiusInches = 0.42f;
        public const float SteerRangeInches = 0.6f;
        public const float MarginInches = 0.25f;

        /// <summary>Touches this far outside a button (as a fraction of its radius) still press it.</summary>
        public const float HitSlack = 1.25f;

        /// <summary>Assumed screen height in inches when the device doesn't report its DPI.</summary>
        public const float FallbackScreenHeightInches = 2.8f;

        public TouchControlLayout(float screenWidth, float screenHeight, float safeLeft, float safeBottom, float safeRight, float safeTop, float dpi)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            float pixelsPerInch = dpi > 0f ? dpi : screenHeight / FallbackScreenHeightInches;
            ButtonRadius = ButtonRadiusInches * pixelsPerInch;
            SteerRange = SteerRangeInches * pixelsPerInch;
            float margin = MarginInches * pixelsPerInch;

            SteerZoneRight = screenWidth / 2f;
            DriftCentre = new Vec2(safeRight - margin - ButtonRadius, safeBottom + margin + ButtonRadius);
            BrakeCentre = new Vec2(DriftCentre.X - 2.4f * ButtonRadius, DriftCentre.Y + 0.4f * ButtonRadius);
            GasCentre = new Vec2(DriftCentre.X, Math.Min(safeTop - margin - ButtonRadius, DriftCentre.Y + 2.6f * ButtonRadius));
        }

        public float ScreenWidth { get; }
        public float ScreenHeight { get; }
        public float ButtonRadius { get; }

        /// <summary>How far, in pixels, the steering thumb drags for full lock.</summary>
        public float SteerRange { get; }

        /// <summary>Touches starting left of this x steer.</summary>
        public float SteerZoneRight { get; }

        public Vec2 DriftCentre { get; }
        public Vec2 BrakeCentre { get; }
        public Vec2 GasCentre { get; }

        public bool HitsButton(Vec2 point, Vec2 centre)
        {
            float dx = point.X - centre.X;
            float dy = point.Y - centre.Y;
            float reach = ButtonRadius * HitSlack;
            return dx * dx + dy * dy <= reach * reach;
        }
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
    /// keeps its last button, so slipping off doesn't let go.
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
        }

        private readonly Dictionary<int, Role> _roles = new Dictionary<int, Role>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly TouchSteering _steering = new TouchSteering();
        private int _steeringFinger = -1;

        public bool DriftHeld { get; private set; }
        public bool BrakeHeld { get; private set; }
        public bool GasHeld { get; private set; }

        public DriverControls Update(IReadOnlyList<TouchPoint> touches, TouchControlLayout layout, bool showGas)
        {
            _seen.Clear();
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
                    role = Assign(touch, layout, showGas);
                }
                else if (role != Role.Steer)
                {
                    var under = ButtonAt(touch.Position, layout, showGas);
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

            DriftHeld = BrakeHeld = GasHeld = false;
            foreach (var role in _roles.Values)
            {
                DriftHeld |= role == Role.Drift;
                BrakeHeld |= role == Role.Brake;
                GasHeld |= role == Role.Gas;
            }

            return new DriverControls
            {
                Steer = _steering.Steer,
                Accelerate = GasHeld ? 1f : 0f,
                Brake = BrakeHeld ? 1f : 0f,
                Drift = DriftHeld,
            };
        }

        public bool Steering => _steering.Active;

        private Role Assign(TouchPoint touch, TouchControlLayout layout, bool showGas)
        {
            var button = ButtonAt(touch.Position, layout, showGas);
            if (button != Role.None)
            {
                return button;
            }

            if (touch.Position.X < layout.SteerZoneRight && _steeringFinger < 0)
            {
                _steeringFinger = touch.Id;
                _steering.Begin(touch.Position.X);
                return Role.Steer;
            }

            return Role.None;
        }

        /// <summary>The nearest button within reach of <paramref name="point"/>, since the reaches of Drift and Brake overlap.</summary>
        private static Role ButtonAt(Vec2 point, TouchControlLayout layout, bool showGas)
        {
            var best = Role.None;
            float bestDistance = float.MaxValue;
            Consider(point, layout, layout.DriftCentre, Role.Drift, ref best, ref bestDistance);
            Consider(point, layout, layout.BrakeCentre, Role.Brake, ref best, ref bestDistance);
            if (showGas)
            {
                Consider(point, layout, layout.GasCentre, Role.Gas, ref best, ref bestDistance);
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
