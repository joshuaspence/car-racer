using System.Collections.Generic;
using CarRacer.Core.Arena;
using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Track;
using CarRacer.Game.Arenas;
using CarRacer.Game.Driving;
using CarRacer.Game.Net;
using CarRacer.Game.Platform;
using UnityEngine;

namespace CarRacer.Game.DriveTest
{
    /// <summary>
    /// Temporary M1 screen for tuning the car on real phones: every built-in track (cars are on one
    /// at a time, starting on the oval) with the chase camera, touch/keyboard/gamepad controls and a
    /// small HUD. Alone it holds one car; given a
    /// <see cref="NetworkSession"/> it is a free drive with everyone in the session (see
    /// <see cref="NetworkedCars"/>). Everything is built from code under one root object, so
    /// destroying it cleans up the whole scene; it also destroys the session, which NGO needs on a
    /// root object of its own. Replaced by the race flow later in M1.
    /// </summary>
    public sealed class DriveTestArena : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;

        /// <summary>Each track's grass reaches this far beyond its road (and stays clear of the others; see <c>TrackCatalogPlacementTests</c>).</summary>
        private const float GroundMargin = 60f;

        private static readonly Color RoadColour = new Color(0.32f, 0.33f, 0.36f);
        private static readonly Color WallColour = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color GrassColour = new Color(0.36f, 0.6f, 0.3f);

        private readonly List<Object> _owned = new List<Object>();
        private ICarInputSource _inputSource;
        private PlayerCarInput _playerInput;
        private NetworkSession _session;
        private Texture2D _circle;

        public CarController Car { get; private set; }
        public Color CarColour { get; private set; }
        public ChaseCamera CameraRig { get; private set; }

        /// <summary>The track cars are on: one of <see cref="TrackCatalog.Layouts"/>, all of which are built.</summary>
        public TrackLayout Track => TrackCatalog.Layouts[TrackIndex];

        public int TrackIndex { get; private set; }

        /// <summary>The hosted or joined session, if any.</summary>
        public NetworkSession Session => _session;

        /// <summary>The player's own controls; null when a test drives the car.</summary>
        public PlayerCarInput PlayerInput => _playerInput;

        /// <summary>Shows the Item button (in a race), labelled with the item held, and lets touches press it.</summary>
        public bool ShowItemButton
        {
            get => _playerInput != null && _playerInput.ShowItem;
            set
            {
                if (_playerInput != null)
                {
                    _playerInput.ShowItem = value;
                }
            }
        }

        public string ItemButtonLabel { get; set; } = string.Empty;

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

            foreach (var layout in TrackCatalog.Layouts)
            {
                BuildTrack(layout);
            }

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
            CameraRig.LookBackInput = _playerInput;

            // Until this phone's car exists (a guest waiting to join), look over the track.
            cameraObject.transform.position = new Vector3(45f, 70f, -70f);
            cameraObject.transform.LookAt(new Vector3(45f, 0f, 30f));

            gameObject.AddComponent<RaceHud>().Initialise(this);
            if (startSession == null)
            {
                SpawnLocalCar(0, CarColours.ForIndex(0));
            }
            else
            {
                _session = startSession();
                ArenaVenues.Build(transform, _session);
                gameObject.AddComponent<NetworkedCars>().Initialise(this, _session);
            }
        }

        /// <summary>Builds a track's road, walls and its own patch of grass, reaching <see cref="GroundMargin"/> beyond the road.</summary>
        private void BuildTrack(TrackLayout layout)
        {
            string name = layout.Definition.DisplayName;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var sample in layout.Spline.SampleEvenly(4f))
            {
                minX = Mathf.Min(minX, sample.Position.X);
                maxX = Mathf.Max(maxX, sample.Position.X);
                minZ = Mathf.Min(minZ, sample.Position.Z);
                maxZ = Mathf.Max(maxZ, sample.Position.Z);
            }

            // Unity's plane is 10 m square.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = name + " ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3((minX + maxX) / 2f, -0.05f, (minZ + maxZ) / 2f);
            ground.transform.localScale = new Vector3((maxX - minX + 2f * GroundMargin) / 10f, 1f, (maxZ - minZ + 2f * GroundMargin) / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = SimpleMaterials.Get(GrassColour);

            var meshes = RoadMeshBuilder.Build(layout.Spline, layout.Definition.WallHeight);
            AddSurface(name + " road", meshes.Road, RoadColour);
            AddSurface(name + " left wall", meshes.LeftWall, WallColour);
            AddSurface(name + " right wall", meshes.RightWall, WallColour);
        }

        /// <summary>
        /// Moves racing to track <paramref name="index"/> (clamped to the catalog). Cars aren't moved
        /// here; they go to the new track's grid when they line up. Returns true if the track changed.
        /// </summary>
        public bool UseTrack(int index)
        {
            index = TrackCatalog.Clamp(index);
            if (index == TrackIndex)
            {
                return false;
            }

            TrackIndex = index;
            return true;
        }

        /// <summary>Switches to the track of the race under way, if any; called by everything that lines cars up, so whichever runs first wins.</summary>
        public void FollowEventTrack(IRaceView race)
        {
            if (race != null && race.Phase != RacePhase.Lobby && race.Mode == GameMode.Race)
            {
                UseTrack(race.TrackIndex);
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
            CarColour = colour;
            Car.InputSource = _inputSource;
            CameraRig.Target = Car;
            PlaceLocalCarOnGrid(slot);
        }

        /// <summary>Puts this phone's car back at rest on grid slot <paramref name="slot"/>, e.g. for a race start.</summary>
        public void PlaceLocalCarOnGrid(int slot) => PlaceLocalCarAtStart(slot, GameMode.Race);

        /// <summary>
        /// Puts this phone's car at rest on its starting spot for <paramref name="mode"/>: grid slot
        /// <paramref name="slot"/> on the track, or spot <paramref name="slot"/> round an arena, and
        /// switches to the overhead camera in an arena (design doc §6).
        /// </summary>
        public void PlaceLocalCarAtStart(int slot, GameMode mode)
        {
            PlaceAtStart(Car, slot, mode);
            CameraRig.ArenaCentre = mode == GameMode.Race ? (Vector3?)null : ArenaLayout.CentreFor(mode).ToUnity();
            CameraRig.Snap();
        }

        /// <summary>Puts <paramref name="car"/> at rest on starting spot <paramref name="slot"/> for <paramref name="mode"/>.</summary>
        public void PlaceAtStart(CarController car, int slot, GameMode mode)
        {
            Vec3 position;
            Vec3 forward;
            if (mode == GameMode.Race)
            {
                var grid = Track.StartGrid(slot + 1)[slot];
                position = grid.Position;
                forward = grid.Forward;
            }
            else
            {
                var spot = ArenaLayout.StartSlotFor(mode, slot);
                position = spot.Position;
                forward = spot.Forward;
            }

            car.Place(position.ToUnity() + Vector3.up * (CarController.SphereRadius + 0.1f), Mathf.Atan2(forward.X, forward.Z));
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

            if (ShowItemButton)
            {
                DrawButton(layout, layout.ItemCentre, ItemButtonLabel, touch.ItemHeld);
            }

            if (_playerInput.ShowLookBack)
            {
                DrawButton(layout, layout.LookBackCentre, "LOOK BACK", touch.LookBackHeld);
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
