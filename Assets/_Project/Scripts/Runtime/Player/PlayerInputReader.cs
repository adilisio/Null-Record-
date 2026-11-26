using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Reads the "Player" action map and exposes per-frame input state.
    /// Runs before other player scripts so they all see the same frame's input.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Leave empty to use the project-wide actions (Project Settings > Input System).")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private float jumpBufferTime = 0.12f;

        private InputAction _move;
        private InputAction _jump;
        private InputAction _crouch;
        private InputAction _sprint;
        private InputAction _interact;
        private float _jumpBufferedUntil = -1f;
        private bool _crouchLatched;
        private float _fullStickTime;
        private bool _autoRun;

        /// <summary>Touch screens: CROUCH toggles (a second tap or a jump stands up) and a fully pushed stick breaks into a run.</summary>
        public static bool TouchStyle => Game.UI.TouchControls.ShouldShow;
        /// <summary>How long the stick must be pushed fully before a touch player starts running.</summary>
        public const float AutoRunDelay = 0.5f;

        /// <summary>The "Player" action map, for systems that read their own actions (e.g. combat).</summary>
        public InputActionMap Map { get; private set; }
        public Vector2 Move { get; private set; }
        public bool JumpHeld => _jump != null && _jump.IsPressed();
        public bool CrouchHeld => (_crouch != null && _crouch.IsPressed() && !TouchStyle) || _crouchLatched;
        public bool SprintHeld => (_sprint != null && _sprint.IsPressed()) || _autoRun;
        public bool CrouchLatched => _crouchLatched;

        /// <summary>Drops a touch crouch toggle (jumping, losing control to a fight, dialogue or a lift).</summary>
        public void ClearCrouchLatch() => _crouchLatched = false;
        public bool InteractPressed { get; private set; }
        public bool HasBufferedJump => Time.time <= _jumpBufferedUntil;

        public void ConsumeJump() => _jumpBufferedUntil = -1f;

        private void Awake()
        {
            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("[PlayerInputReader] No InputActionAsset assigned and no project-wide actions set.", this);
                enabled = false;
                return;
            }

            InputActionMap map = asset.FindActionMap("Player", throwIfNotFound: true);
            Map = map;
            _move = map.FindAction("Move", true);
            _jump = map.FindAction("Jump", true);
            _crouch = map.FindAction("Crouch", true);
            _sprint = map.FindAction("Sprint", true);
            _interact = map.FindAction("Interact", true);
            map.Enable();
        }

        private void OnDisable()
        {
            Move = Vector2.zero;
            InteractPressed = false;
            _jumpBufferedUntil = -1f;
            _crouchLatched = false;
            _autoRun = false;
        }

        private void Update()
        {
            Move = _move.ReadValue<Vector2>();
            if (_jump.WasPressedThisFrame())
            {
                _jumpBufferedUntil = Time.time + jumpBufferTime;
            }
            InteractPressed = _interact.WasPressedThisFrame();

            bool touch = TouchStyle;
            if (touch && _crouch.WasPressedThisFrame()) _crouchLatched = !_crouchLatched;
            if (!touch) _crouchLatched = false;
            if (touch && Mathf.Abs(Move.x) > 0.85f) _fullStickTime += Time.unscaledDeltaTime;
            else _fullStickTime = 0f;
            _autoRun = touch && _fullStickTime >= AutoRunDelay;
        }
    }
}
