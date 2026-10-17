using CarRacer.Core.Driving;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Drives a camera to follow a car (design doc §6): behind it with <see cref="ChaseCameraRig"/>
    /// on the track, or from high above with <see cref="OverheadCameraRig"/> in an arena.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public ChaseCameraSettings Settings = new ChaseCameraSettings();
        public OverheadCameraSettings OverheadSettings = new OverheadCameraSettings();
        public CarController Target;

        /// <summary>Set to an arena's centre for the overhead camera; null for the chase camera.</summary>
        public Vector3? ArenaCentre;

        private Camera _camera;
        private ChaseCameraState _state;

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

            // Follow the interpolated transform, not the rigidbody, so the camera moves smoothly between physics steps.
            _state = ArenaCentre.HasValue
                ? OverheadCameraRig.Step(_state, Target.transform.position.ToCore(), ArenaCentre.Value.ToCore(), Time.deltaTime, OverheadSettings)
                : ChaseCameraRig.Step(_state, Target.transform.position.ToCore(), Target.State.Heading, Target.ForwardSpeed, Time.deltaTime, Settings);
            transform.position = _state.Position.ToUnity();
            transform.LookAt(_state.LookAt.ToUnity());
            _camera.fieldOfView = _state.FieldOfView;
        }
    }
}
