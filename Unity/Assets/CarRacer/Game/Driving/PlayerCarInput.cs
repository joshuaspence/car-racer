using System.Collections.Generic;
using CarRacer.Core.Driving;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// The local player's controls (design doc §5): on-screen touch controls, keyboard and gamepad,
    /// all usable at once. Reads devices every frame; the car samples <see cref="Read"/> each physics step.
    /// Uses the Input System when it is the active input handler, otherwise the legacy Input Manager.
    /// </summary>
    public sealed class PlayerCarInput : MonoBehaviour, ICarInputSource
    {
        public const string AutoAccelerateKey = "autoAccelerate";

        private readonly TouchControls _touchControls = new TouchControls();
        private readonly List<TouchPoint> _touches = new List<TouchPoint>();
        private CarInput _current;
        private bool _autoAccelerate;
        private bool _itemPressed;

        public TouchControls TouchState => _touchControls;

        /// <summary>Whether the Item button is on screen (in a race); while it isn't, its spot takes no fingers.</summary>
        public bool ShowItem { get; set; }
        public TouchControlLayout Layout { get; private set; }

        public bool AutoAccelerate
        {
            get => _autoAccelerate;
            set
            {
                _autoAccelerate = value;
                PlayerPrefs.SetInt(AutoAccelerateKey, value ? 1 : 0);
            }
        }

        public CarInput Read() => _current;

        /// <summary>True once per press of the use-item control (Item button, E, or a gamepad's Y/X), then false until the next press.</summary>
        public bool ConsumeItemPress()
        {
            bool pressed = _itemPressed;
            _itemPressed = false;
            return pressed;
        }

        private void Awake()
        {
            _autoAccelerate = PlayerPrefs.GetInt(AutoAccelerateKey, 1) != 0;
        }

        private void Update()
        {
            var safe = Screen.safeArea;
            if (Layout == null || Layout.ScreenWidth != Screen.width || Layout.ScreenHeight != Screen.height)
            {
                Layout = new TouchControlLayout(Screen.width, Screen.height, safe.xMin, safe.yMin, safe.xMax, safe.yMax, Screen.dpi);
            }

            ReadTouches(_touches);
            var touch = _touchControls.Update(_touches, Layout, showGas: !_autoAccelerate, showItem: ShowItem);
            var devices = ReadDevices();
            _current = DriverInput.Combine(touch, devices, _autoAccelerate);
            _itemPressed |= touch.UseItem || devices.UseItem;
        }

#if ENABLE_INPUT_SYSTEM
        private static void ReadTouches(List<TouchPoint> touches)
        {
            touches.Clear();
            var screen = Touchscreen.current;
            if (screen == null)
            {
                return;
            }

            foreach (var touch in screen.touches)
            {
                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None)
                {
                    continue;
                }

                var stage = phase == UnityEngine.InputSystem.TouchPhase.Began ? TouchStage.Began
                    : phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled ? TouchStage.Ended
                    : TouchStage.Held;
                var position = touch.position.ReadValue();
                touches.Add(new TouchPoint(touch.touchId.ReadValue(), position.x, position.y, stage));
            }
        }

        private static DriverControls ReadDevices()
        {
            var controls = new DriverControls();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool left = keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
                bool right = keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
                controls.Steer = (right ? 1f : 0f) - (left ? 1f : 0f);
                controls.Accelerate = keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f;
                controls.Brake = keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f;
                controls.Drift = keyboard.spaceKey.isPressed || keyboard.leftShiftKey.isPressed;
                controls.UseItem = keyboard.eKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame;
            }

            // Left stick steers; A or the right trigger accelerates; B or the left trigger brakes; either shoulder drifts; Y or X uses the item.
            var pad = Gamepad.current;
            if (pad != null)
            {
                float steer = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(steer) > Mathf.Abs(controls.Steer))
                {
                    controls.Steer = steer;
                }

                controls.Accelerate = Mathf.Max(controls.Accelerate, Mathf.Max(pad.rightTrigger.ReadValue(), pad.buttonSouth.isPressed ? 1f : 0f));
                controls.Brake = Mathf.Max(controls.Brake, Mathf.Max(pad.leftTrigger.ReadValue(), pad.buttonEast.isPressed ? 1f : 0f));
                controls.Drift |= pad.rightShoulder.isPressed || pad.leftShoulder.isPressed;
                controls.UseItem |= pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
            }

            return controls;
        }
#else
        private static void ReadTouches(List<TouchPoint> touches)
        {
            touches.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                var stage = touch.phase == UnityEngine.TouchPhase.Began ? TouchStage.Began
                    : touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled ? TouchStage.Ended
                    : TouchStage.Held;
                touches.Add(new TouchPoint(touch.fingerId, touch.position.x, touch.position.y, stage));
            }
        }

        private static DriverControls ReadDevices()
        {
            // The default Input Manager axes cover the arrow keys, WASD and a gamepad's left stick.
            return new DriverControls
            {
                Steer = Input.GetAxisRaw("Horizontal"),
                Accelerate = Mathf.Max(0f, Input.GetAxisRaw("Vertical")),
                Brake = Mathf.Max(0f, -Input.GetAxisRaw("Vertical")),
                Drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton5) || Input.GetKey(KeyCode.JoystickButton4),
                UseItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.JoystickButton2),
            };
        }
#endif
    }
}
