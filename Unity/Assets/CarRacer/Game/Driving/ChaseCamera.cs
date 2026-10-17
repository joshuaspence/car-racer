using CarRacer.Core.Driving;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>Drives a camera with <see cref="ChaseCameraRig"/> to follow a car (design doc §6).</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public ChaseCameraSettings Settings = new ChaseCameraSettings();
        public CarController Target;

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
            _state = ChaseCameraRig.Step(_state, Target.transform.position.ToCore(), Target.State.Heading, Target.ForwardSpeed, Time.deltaTime, Settings);
            transform.position = _state.Position.ToUnity();
            transform.LookAt(_state.LookAt.ToUnity());
            _camera.fieldOfView = _state.FieldOfView;
        }
    }
}
