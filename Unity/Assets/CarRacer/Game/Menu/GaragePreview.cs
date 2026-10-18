using CarRacer.Core.Art;
using CarRacer.Game.Driving;
using UnityEngine;

namespace CarRacer.Game.Menu
{
    /// <summary>
    /// The garage's turntable (design doc §11): the chosen body in the chosen colour, turning
    /// slowly, filmed by its own camera into a texture the menu draws. It sits far below the menu
    /// so nothing else is in shot, and lights itself.
    /// </summary>
    public sealed class GaragePreview : MonoBehaviour
    {
        private const float TurnDegreesPerSecond = 30f;

        private static readonly Vector3 Stage = new Vector3(0f, -1000f, 0f);

        private Transform _turntable;
        private Transform _car;
        private Camera _camera;
        private CarModel _model;
        private Color _colour;

        /// <summary>What the preview camera sees, for <c>GUI.DrawTexture</c>.</summary>
        public RenderTexture Texture { get; private set; }

        private void Awake()
        {
            Texture = new RenderTexture(512, 384, 16) { name = "Garage preview" };

            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(transform, false);
            _turntable.position = Stage;

            var cameraObject = new GameObject("Garage camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = Stage + new Vector3(0f, 1.8f, -4.6f);
            cameraObject.transform.LookAt(Stage + new Vector3(0f, 0.5f, 0f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.targetTexture = Texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = UiStyle.Background;
            _camera.fieldOfView = 35f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 20f;

            var light = new GameObject("Garage light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.transform.SetParent(transform, false);
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        /// <summary>Shows <paramref name="model"/> in <paramref name="colour"/>, rebuilding the car only when either changes.</summary>
        public void Show(CarModel model, Color colour)
        {
            if (_car != null && model == _model && colour == _colour)
            {
                return;
            }

            if (_car != null)
            {
                Destroy(_car.gameObject);
            }

            _model = model;
            _colour = colour;
            _car = new GameObject("Car").transform;
            _car.SetParent(_turntable, false);
            if (!CarBodies.TryBuild(_car, colour, model))
            {
                SimpleMaterials.Part(PrimitiveType.Cube, _car, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.6f, 2.3f), SimpleMaterials.Get(colour));
            }
        }

        private void Update()
        {
            _turntable.Rotate(0f, TurnDegreesPerSecond * Time.unscaledDeltaTime, 0f);
        }

        private void OnDestroy()
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }
        }
    }
}
