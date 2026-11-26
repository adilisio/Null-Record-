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

        public Vector2 Move { get; private set; }
        public bool JumpHeld => _jump != null && _jump.IsPressed();
        public bool CrouchHeld => _crouch != null && _crouch.IsPressed();
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
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
        }

        private void Update()
        {
            Move = _move.ReadValue<Vector2>();
            if (_jump.WasPressedThisFrame())
            {
                _jumpBufferedUntil = Time.time + jumpBufferTime;
            }
            InteractPressed = _interact.WasPressedThisFrame();
        }
    }
}
