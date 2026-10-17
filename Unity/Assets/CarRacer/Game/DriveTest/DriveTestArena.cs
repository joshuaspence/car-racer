using System.Collections.Generic;
using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using CarRacer.Core.Track;
using CarRacer.Game.Driving;
using CarRacer.Game.Net;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.DriveTest
{
    /// <summary>
    /// Temporary M1 screen for tuning the car on real phones: the sample oval with the chase camera,
    /// touch/keyboard/gamepad controls and a small HUD. Alone it holds one car; given a
    /// <see cref="NetworkSession"/> it is a free drive with everyone in the session (see
    /// <see cref="NetworkedCars"/>). Everything is built from code under one root object, so
    /// destroying it cleans up the whole scene; it also destroys the session, which NGO needs on a
    /// root object of its own. Replaced by the race flow later in M1.
    /// </summary>
    public sealed class DriveTestArena : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;

        private static readonly Color RoadColour = new Color(0.32f, 0.33f, 0.36f);
        private static readonly Color WallColour = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color GrassColour = new Color(0.36f, 0.6f, 0.3f);

        private readonly List<Object> _owned = new List<Object>();
        private ICarInputSource _inputSource;
        private PlayerCarInput _playerInput;
        private NetworkSession _session;
        private Texture2D _circle;

        public CarController Car { get; private set; }
        public ChaseCamera CameraRig { get; private set; }
        public TrackLayout Track { get; private set; }

        /// <summary>The hosted or joined session, if any.</summary>
        public NetworkSession Session => _session;

        /// <summary>
        /// Builds the arena. Pass <paramref name="inputOverride"/> to drive the car from a test instead
        /// of the player's devices, and <paramref name="startSession"/> to start a hosted or joined
        /// session (destroyed with the arena).
        /// </summary>
        public static DriveTestArena Create(ICarInputSource inputOverride = null, System.Func<NetworkSession> startSession = null)
        {
            var arena = new GameObject("Drive test").AddComponent<DriveTestArena>();
            arena.Build(inputOverride, startSession);
            return arena;
        }

        private void Build(ICarInputSource inputOverride, System.Func<NetworkSession> startSession)
        {
            Time.fixedDeltaTime = 0.02f;

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.transform.SetParent(transform, false);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(45f, -0.05f, 30f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = SimpleMaterials.Get(GrassColour);

            Track = new TrackLayout(SampleTracks.Oval());
            var meshes = RoadMeshBuilder.Build(Track.Spline, Track.Definition.WallHeight);
            AddSurface("Road", meshes.Road, RoadColour);
            AddSurface("Left wall", meshes.LeftWall, WallColour);
            AddSurface("Right wall", meshes.RightWall, WallColour);

            if (inputOverride != null)
            {
                _inputSource = inputOverride;
            }
            else
            {
                _playerInput = gameObject.AddComponent<PlayerCarInput>();
                _inputSource = _playerInput;
            }

            var cameraObject = new GameObject("Chase camera");
            cameraObject.transform.SetParent(transform, false);
            var unityCamera = cameraObject.AddComponent<Camera>();
            unityCamera.tag = "MainCamera";
            unityCamera.clearFlags = CameraClearFlags.SolidColor;
            unityCamera.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            unityCamera.nearClipPlane = 0.3f;
            unityCamera.farClipPlane = 600f;
            CameraRig = cameraObject.AddComponent<ChaseCamera>();

            // Until this phone's car exists (a guest waiting to join), look over the track.
            cameraObject.transform.position = new Vector3(45f, 70f, -70f);
            cameraObject.transform.LookAt(new Vector3(45f, 0f, 30f));

            if (startSession == null)
            {
                SpawnLocalCar(0, CarColours.ForIndex(0));
            }
            else
            {
                _session = startSession();
                gameObject.AddComponent<NetworkedCars>().Initialise(this, _session);
            }
        }

        /// <summary>Creates this phone's car on grid slot <paramref name="slot"/> and points the camera at it.</summary>
        public void SpawnLocalCar(int slot, Color colour)
        {
            if (Car != null)
            {
                Destroy(Car.gameObject);
            }

            Car = CarFactory.Build("Car", colour, transform);
            Car.InputSource = _inputSource;
            CameraRig.Target = Car;
            PlaceLocalCarOnGrid(slot);
        }

        /// <summary>Puts this phone's car back at rest on grid slot <paramref name="slot"/>, e.g. for a race start.</summary>
        public void PlaceLocalCarOnGrid(int slot)
        {
            var grid = Track.StartGrid(slot + 1)[slot];
            Vector3 spawn = grid.Position.ToUnity() + Vector3.up * (CarController.SphereRadius + 0.1f);
            Car.Place(spawn, Mathf.Atan2(grid.Forward.X, grid.Forward.Z));
            CameraRig.Snap();
        }

        private void AddSurface(string name, MeshData data, Color colour)
        {
            var mesh = data.ToUnityMesh(name);
            _owned.Add(mesh);
            var surface = new GameObject(name);
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = SimpleMaterials.Get(colour);
            surface.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            foreach (var owned in _owned)
            {
                Destroy(owned);
            }

            if (_circle != null)
            {
                Destroy(_circle);
            }

            if (_session != null)
            {
                Destroy(_session.gameObject);
            }
        }

        private void OnGUI()
        {
            if (_playerInput != null)
            {
                DrawTouchControls();
            }

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var safe = Screen.safeArea;
            float left = safe.x / scale + 24f;
            float width = Screen.width / scale;

            GUI.skin.label.fontSize = 26;
            GUI.skin.button.fontSize = 22;

            if (Car != null)
            {
                var state = Car.State;
                int tier = state.DriftTier(Car.Tuning);
                string drift = state.Drifting ? $"Drift {tier}/{Car.Tuning.DriftTierSeconds.Length}" : string.Empty;
                string boost = state.BoostSecondsLeft > 0f ? $"BOOST +{state.BoostSpeed:0}" : string.Empty;
                GUI.Label(new Rect(left, 20f, 600f, 40f), $"{Mathf.Abs(Car.ForwardSpeed) * 3.6f:0} km/h  {drift}  {boost}");
            }

            if (GUI.Button(new Rect(width - 160f, 20f, 136f, 52f), "Back"))
            {
                Bootstrap.ShowMainMenu();
            }

            if (_playerInput != null)
            {
                string label = _playerInput.AutoAccelerate ? "Auto-accelerate: on" : "Auto-accelerate: off";
                if (GUI.Button(new Rect(width - 420f, 20f, 248f, 52f), label))
                {
                    _playerInput.AutoAccelerate = !_playerInput.AutoAccelerate;
                }

                if (GUI.Button(new Rect(width - 600f, 20f, 168f, 52f), Vibration.Enabled ? "Haptics: on" : "Haptics: off"))
                {
                    Vibration.Enabled = !Vibration.Enabled;
                }
            }
        }

        private void DrawTouchControls()
        {
            var layout = _playerInput.Layout;
            if (layout == null)
            {
                return;
            }

            GUI.matrix = Matrix4x4.identity;
            var touch = _playerInput.TouchState;
            DrawButton(layout, layout.DriftCentre, "DRIFT", touch.DriftHeld);
            DrawButton(layout, layout.BrakeCentre, "BRAKE", touch.BrakeHeld);
            if (!_playerInput.AutoAccelerate)
            {
                DrawButton(layout, layout.GasCentre, "GAS", touch.GasHeld);
            }
        }

        private void DrawButton(TouchControlLayout layout, Vec2 centre, string label, bool held)
        {
            // Touch coordinates start bottom-left; IMGUI's start top-left.
            float r = layout.ButtonRadius;
            var rect = new Rect(centre.X - r, Screen.height - centre.Y - r, 2f * r, 2f * r);
            var previous = GUI.color;
            GUI.color = held ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(rect, Circle());
            GUI.color = previous;

            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(r * 0.32f), fontStyle = FontStyle.Bold };
            GUI.Label(rect, label, style);
        }

        private Texture2D Circle()
        {
            if (_circle == null)
            {
                const int size = 128;
                _circle = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                float c = (size - 1) / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        byte alpha = (byte)(Mathf.Clamp01(c - d) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                _circle.SetPixels32(pixels);
                _circle.Apply();
            }

            return _circle;
        }
    }
}
