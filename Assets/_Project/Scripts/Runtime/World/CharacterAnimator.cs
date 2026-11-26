using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Drives a character model's Animator (Idle / Run / Jump) from how fast it actually moves, so the same
    /// component works for the player, the companion follower and combat units moved by the encounter.
    /// Lives on the root of a character prefab (see <c>CharacterArt</c> in the editor tools).
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int GroundedId = Animator.StringToHash("Grounded");
        public static readonly int CrouchId = Animator.StringToHash("Crouch");

        [SerializeField] private Animator animator;
        [Tooltip("Moves further than this in one frame count as a teleport, not running.")]
        [SerializeField] private float teleportDistance = 1.5f;

        private PlayerMotor _motor;
        private float _holdUntil;
        private Vector3 _last;
        private float _speed;

        public Animator Animator => animator;
        /// <summary>Smoothed horizontal speed in m/s, as fed to the Animator.</summary>
        public float Speed => _speed;

        public void Configure(Animator target) => animator = target;

        /// <summary>The last action fired with <see cref="Play"/> (for tests and debugging).</summary>
        public string LastAction { get; private set; } = "";

        /// <summary>
        /// Fires a one-shot action clip (Strike, Shove, Swing, Shoot, Hit). Movement is ignored while it plays,
        /// so a combat lunge doesn't flip the character into Run.
        /// </summary>
        public void Play(string action, float hold = 0.5f)
        {
            if (string.IsNullOrEmpty(action) || animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetTrigger(action);
            LastAction = action;
            _holdUntil = Time.time + hold;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _motor = GetComponentInParent<PlayerMotor>();
            _last = transform.position;
        }

        private void OnEnable() => _last = transform.position;

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float moved = Mathf.Abs(p.x - _last.x);
                float v = moved > teleportDistance || Time.time < _holdUntil ? 0f : moved / dt;
                _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-dt * 14f));
            }
            _last = p;

            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(SpeedId, _speed);
            animator.SetBool(GroundedId, _motor == null || _motor.State != MotorState.Airborne);
            animator.SetBool(CrouchId, _motor != null && _motor.IsCrouching);
        }
    }
}
