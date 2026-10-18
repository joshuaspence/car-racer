using CarRacer.Core.Driving;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Drives a camera to follow a car (design doc §6): behind it with <see cref="ChaseCameraRig"/>
    /// on the track, turned round while the player holds the rear-view glance, or from high above
    /// with <see cref="OverheadCameraRig"/> in an arena.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public ChaseCameraSettings Settings = new ChaseCameraSettings();
        public OverheadCameraSettings OverheadSettings = new OverheadCameraSettings();
        public CarController Target;

        /// <summary>Set to an arena's centre for the overhead camera; null for the chase camera.</summary>
        public Vector3? ArenaCentre;

        /// <summary>The player's controls, for the rear-view glance; null when a test drives the car.</summary>
        public PlayerCarInput LookBackInput;

        /// <summary>The camera is looking back past the car this frame.</summary>
        public bool LookingBack { get; private set; }

        private Camera _camera;
        private ChaseCameraState _state;
        private ChaseCameraSettings _calm;
        private ChaseCameraSettings _calmFrom;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        /// <summary>Jumps straight to the resting position behind the target, e.g. after a respawn.</summary>
        public void Snap()
        {
            _state = default;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            // The overhead camera sees everyone already, so the glance is for the chase camera only.
            bool chasing = !ArenaCentre.HasValue;
            if (LookBackInput != null)
            {
                LookBackInput.ShowLookBack = chasing;
            }

            LookingBack = chasing && LookBackInput != null && LookBackInput.LookingBack;

            // Reduced motion (design doc §6) keeps the framing but drops the speed zoom.
            var settings = Settings;
            if (Accessibility.ReducedMotion)
            {
                if (_calmFrom != Settings)
                {
                    _calm = Settings.WithReducedMotion();
                    _calmFrom = Settings;
                }

                settings = _calm;
            }

            // Follow the interpolated transform, not the rigidbody, so the camera moves smoothly between physics steps.
            var target = Target.transform.position.ToCore();
            _state = chasing
                ? ChaseCameraRig.Step(_state, target, Target.State.Heading, Target.ForwardSpeed, Time.deltaTime, settings)
                : OverheadCameraRig.Step(_state, target, ArenaCentre.Value.ToCore(), Time.deltaTime, OverheadSettings);

            // The chase camera keeps following during a glance, so letting go cuts straight back to it.
            var shown = LookingBack ? ChaseCameraRig.RearView(_state, target, Target.State.Heading, Target.ForwardSpeed, settings) : _state;
            transform.position = shown.Position.ToUnity();
            transform.LookAt(shown.LookAt.ToUnity());
            _camera.fieldOfView = shown.FieldOfView;
        }
    }
}
