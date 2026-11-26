using System;
using Game.World;
using UnityEngine;

namespace Game.Player
{
    public enum MotorState
    {
        Grounded,
        Airborne,
        Ladder,
        LedgeHang,
        LedgeClimb,
    }

    /// <summary>
    /// Side-on (2.5D) character motor. Movement happens on the X/Y plane; Z is held on <see cref="laneZ"/>.
    /// The transform pivot is at the character's feet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Lane")]
        [SerializeField] private float laneZ = 0f;

        [Header("Run")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float groundAcceleration = 60f;
        [SerializeField] private float airAcceleration = 30f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.6f;
        [SerializeField] private float gravity = -30f;
        [SerializeField] private float fallGravityMultiplier = 1.6f;
        [Tooltip("Upward speed is multiplied by this when jump is released early.")]
        [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;
        [SerializeField] private float maxFallSpeed = 20f;
        [SerializeField] private float coyoteTime = 0.1f;

        [Header("Body")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [Tooltip("Optional visual that is scaled to match the collider height.")]
        [SerializeField] private Transform body;

        [Header("Ladder")]
        [SerializeField] private float climbSpeed = 3f;

        [Header("Ledge")]
        [SerializeField] private LayerMask environmentMask = ~0;
        [SerializeField] private float ledgeReach = 0.35f;
        [SerializeField] private float ledgeClimbDuration = 0.35f;

        private readonly Collider[] _overlap = new Collider[8];
        private CharacterController _cc;
        private PlayerInputReader _input;
        private Vector3 _velocity;
        private float _lastGroundedTime = float.NegativeInfinity;
        private bool _jumpCutApplied;
        private float _ledgeRegrabBlockedUntil;
        private float _ladderRegrabBlockedUntil;
        private Ladder _ladder;
        private Vector3 _ledgeStandPos;
        private Vector3 _climbFrom;
        private float _climbT;
        private float _bodyWidth = 1f;

        public MotorState State { get; private set; } = MotorState.Airborne;
        public int Facing { get; private set; } = 1;
        public bool IsCrouching { get; private set; }
        public Vector3 Velocity => _velocity;
        public float LaneZ => laneZ;

        /// <summary>When false, the motor still applies gravity but ignores player input.</summary>
        public bool ControlEnabled { get; set; } = true;

        /// <summary>True while another system (combat) moves the transform directly. The motor does nothing.</summary>
        public bool ExternallyControlled { get; private set; }

        public event Action<MotorState> StateChanged;

        private float JumpVelocity => Mathf.Sqrt(2f * jumpHeight * -gravity);

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            // Default 0.001 drops tiny per-frame moves at high frame rates (player crawls, jumps fail).
            _cc.minMoveDistance = 0f;
            _input = GetComponent<PlayerInputReader>();
            if (body != null) _bodyWidth = body.localScale.x;
            ApplyHeight(standHeight);
            transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        private void Update()
        {
            if (ExternallyControlled) return;
            float dt = Time.deltaTime;
            Vector2 move = ControlEnabled ? _input.Move : Vector2.zero;

            switch (State)
            {
                case MotorState.Ladder:
                    TickLadder(move, dt);
                    break;
                case MotorState.LedgeHang:
                    TickLedgeHang(move);
                    break;
                case MotorState.LedgeClimb:
                    TickLedgeClimb(dt);
                    break;
                default:
                    TickGroundAndAir(move, dt);
                    break;
            }
        }

        /// <summary>Moves the character instantly (respawns, cutscenes, encounter snapping).</summary>
        public void Teleport(Vector3 position, bool resetVelocity = true)
        {
            position.z = laneZ;
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = !ExternallyControlled;
            if (resetVelocity) _velocity = Vector3.zero;
            SetState(MotorState.Airborne);
        }

        /// <summary>
        /// Hands the transform to another system (e.g. an encounter): disables the CharacterController and input.
        /// Turning it off returns control and lets the player fall onto whatever is below.
        /// </summary>
        public void SetExternalControl(bool on)
        {
            if (on == ExternallyControlled) return;
            ExternallyControlled = on;
            ControlEnabled = !on;
            _velocity = Vector3.zero;
            _cc.enabled = !on;
            if (!on) SetState(MotorState.Airborne);
        }

        /// <summary>Turns to face +X (1) or -X (-1).</summary>
        public void Face(int dir) => SetFacing(dir >= 0 ? 1 : -1);

        // ---------------------------------------------------------------- ground / air

        private void TickGroundAndAir(Vector2 move, float dt)
        {
            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                _lastGroundedTime = Time.time;
                if (_velocity.y < 0f) _velocity.y = -2f;
            }

            if (Mathf.Abs(move.x) > 0.1f) SetFacing(move.x > 0f ? 1 : -1);

            SetCrouch(ControlEnabled && grounded && _input.CrouchHeld);

            float targetSpeed = IsCrouching ? crouchSpeed : (_input.SprintHeld ? sprintSpeed : walkSpeed);
            float accel = grounded ? groundAcceleration : airAcceleration;
            _velocity.x = Mathf.MoveTowards(_velocity.x, move.x * targetSpeed, accel * dt);

            if (ControlEnabled && TryEnterLadder(move, grounded)) return;

            bool inCoyote = Time.time - _lastGroundedTime <= coyoteTime;
            if (ControlEnabled && _input.HasBufferedJump && inCoyote && !IsCrouching)
            {
                _input.ConsumeJump();
                _velocity.y = JumpVelocity;
                _lastGroundedTime = float.NegativeInfinity;
                _jumpCutApplied = false;
            }

            if (_velocity.y > 0f && !_jumpCutApplied && !_input.JumpHeld)
            {
                _velocity.y *= jumpCutMultiplier;
                _jumpCutApplied = true;
            }

            float g = gravity * (_velocity.y < 0f ? fallGravityMultiplier : 1f);
            _velocity.y = Mathf.Max(_velocity.y + g * dt, -maxFallSpeed);

            Vector3 delta = new Vector3(_velocity.x * dt, _velocity.y * dt, laneZ - transform.position.z);
            CollisionFlags flags = _cc.Move(delta);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0 && Mathf.Sign(_velocity.x) == Facing) _velocity.x = 0f;

            SetState(_cc.isGrounded ? MotorState.Grounded : MotorState.Airborne);

            if (State == MotorState.Airborne
                && ControlEnabled
                && _velocity.y <= 0f
                && move.x * Facing > 0.3f
                && Time.time >= _ledgeRegrabBlockedUntil)
            {
                TryGrabLedge();
            }
        }

        private void SetCrouch(bool wantCrouch)
        {
            if (wantCrouch == IsCrouching) return;
            if (!wantCrouch && !HasHeadroom(transform.position, standHeight)) return;
            IsCrouching = wantCrouch;
            ApplyHeight(wantCrouch ? crouchHeight : standHeight);
        }

        private void ApplyHeight(float h)
        {
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
            if (body != null)
            {
                // Unity's capsule primitive is 2 units tall; scale it to match.
                body.localScale = new Vector3(_bodyWidth, h * 0.5f, _bodyWidth);
                body.localPosition = new Vector3(0f, h * 0.5f, 0f);
            }
        }

        private void SetFacing(int dir)
        {
            if (dir == Facing) return;
            Facing = dir;
            transform.rotation = Quaternion.Euler(0f, dir > 0 ? 90f : -90f, 0f);
        }

        // ---------------------------------------------------------------- ladder

        private bool TryEnterLadder(Vector2 move, bool grounded)
        {
            if (Mathf.Abs(move.y) < 0.5f || Time.time < _ladderRegrabBlockedUntil) return false;

            Vector3 feet = transform.position;
            Ladder ladder = Ladder.Find(feet);
            if (ladder == null) return false;

            bool climbUp = move.y > 0f && feet.y < ladder.Top - 0.1f;
            bool climbDownFromTop = move.y < 0f && grounded && feet.y >= ladder.Top - 0.2f;
            if (!climbUp && !climbDownFromTop) return false;
            if (IsCrouching && !HasHeadroom(feet, standHeight)) return false;

            SetCrouch(false);
            _ladder = ladder;
            _velocity = Vector3.zero;
            float y = climbDownFromTop ? ladder.Top - 0.4f : feet.y;
            Teleport(new Vector3(ladder.X, y, laneZ));
            SetState(MotorState.Ladder);
            return true;
        }

        private void TickLadder(Vector2 move, float dt)
        {
            if (ControlEnabled && _input.HasBufferedJump)
            {
                _input.ConsumeJump();
                LeaveLadder(new Vector3(move.x * walkSpeed, JumpVelocity * 0.7f, 0f));
                return;
            }

            float vy = move.y * climbSpeed;
            _cc.Move(new Vector3(_ladder.X - transform.position.x, vy * dt, laneZ - transform.position.z));

            float feet = transform.position.y;
            if (vy > 0f && feet >= _ladder.Top - 0.05f)
            {
                int dir = _ladder.TopExitDirection;
                SetFacing(dir);
                Teleport(new Vector3(_ladder.X + dir * (_cc.radius + 0.6f), _ladder.Top + 0.05f, laneZ));
                _ladder = null;
                return;
            }

            if (vy < 0f && _cc.isGrounded)
            {
                LeaveLadder(Vector3.zero);
                return;
            }

            if (!_ladder.Contains(transform.position)) LeaveLadder(Vector3.zero);
        }

        private void LeaveLadder(Vector3 velocity)
        {
            _ladder = null;
            _velocity = velocity;
            _jumpCutApplied = false;
            _ladderRegrabBlockedUntil = Time.time + 0.25f;
            SetState(MotorState.Airborne);
        }

        // ---------------------------------------------------------------- ledge

        private void TryGrabLedge()
        {
            Vector3 pos = transform.position;
            Vector3 fwd = Vector3.right * Facing;
            float h = _cc.height;
            float reach = _cc.radius + ledgeReach;
            Vector3 chest = pos + Vector3.up * (h * 0.7f);
            Vector3 aboveHead = pos + Vector3.up * (h + 0.15f);

            if (!Physics.Raycast(chest, fwd, out RaycastHit wall, reach, environmentMask, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(wall.normal.y) > 0.3f) return;
            if (Physics.Raycast(aboveHead, fwd, reach, environmentMask, QueryTriggerInteraction.Ignore)) return;

            Vector3 probe = new Vector3(wall.point.x + Facing * 0.15f, aboveHead.y, laneZ);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, aboveHead.y - chest.y + 0.05f, environmentMask, QueryTriggerInteraction.Ignore)) return;
            if (top.normal.y < 0.7f) return;

            Vector3 standPos = new Vector3(wall.point.x + Facing * (_cc.radius + 0.1f), top.point.y + 0.02f, laneZ);
            if (!HasHeadroom(standPos, standHeight)) return;

            _ledgeStandPos = standPos;
            Vector3 hangPos = new Vector3(wall.point.x - Facing * (_cc.radius + 0.02f), top.point.y - h * 0.9f, laneZ);
            Teleport(hangPos);
            SetState(MotorState.LedgeHang);
        }

        private void TickLedgeHang(Vector2 move)
        {
            if (!ControlEnabled) return;

            if (move.y > 0.5f || _input.HasBufferedJump)
            {
                _input.ConsumeJump();
                _climbFrom = transform.position;
                _climbT = 0f;
                _cc.enabled = false;
                SetState(MotorState.LedgeClimb);
            }
            else if (move.y < -0.5f || move.x * Facing < -0.5f)
            {
                _ledgeRegrabBlockedUntil = Time.time + 0.3f;
                SetState(MotorState.Airborne);
            }
        }

        private void TickLedgeClimb(float dt)
        {
            _climbT = Mathf.Min(1f, _climbT + dt / ledgeClimbDuration);
            // Rise straight up first, then step forward onto the ledge.
            Vector3 corner = new Vector3(_climbFrom.x, _ledgeStandPos.y, laneZ);
            transform.position = _climbT < 0.6f
                ? Vector3.Lerp(_climbFrom, corner, _climbT / 0.6f)
                : Vector3.Lerp(corner, _ledgeStandPos, (_climbT - 0.6f) / 0.4f);

            if (_climbT >= 1f)
            {
                _cc.enabled = true;
                _velocity = Vector3.zero;
                SetState(MotorState.Grounded);
            }
        }

        // ---------------------------------------------------------------- helpers

        private bool HasHeadroom(Vector3 feet, float height)
        {
            float r = _cc.radius * 0.95f;
            Vector3 bottom = feet + Vector3.up * (r + 0.05f);
            Vector3 top = feet + Vector3.up * (height - r);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, _overlap, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_overlap[i] != _cc) return false;
            }
            return true;
        }

        private void SetState(MotorState next)
        {
            if (next == State) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private void OnDrawGizmosSelected()
        {
            CharacterController cc = _cc != null ? _cc : GetComponent<CharacterController>();
            if (cc == null) return;
            Vector3 pos = transform.position;
            float reach = cc.radius + ledgeReach;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(pos + Vector3.up * (cc.height * 0.7f), Vector3.right * Facing * reach);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(pos + Vector3.up * (cc.height + 0.15f), Vector3.right * Facing * reach);
        }
    }
}
