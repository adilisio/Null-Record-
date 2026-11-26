using Game.Player;
using UnityEngine;

namespace Game.Companions
{
    /// <summary>
    /// Keeps a recruited companion trailing the player. Kinematic and collider-free (no CharacterController):
    /// walks along the floor, and when the player gets far ahead or reaches another floor
    /// (ladders, ledges), it catches up by appearing behind them.
    /// </summary>
    public class CompanionFollower : MonoBehaviour
    {
        /// <summary>The companion currently in the party (one at a time), or null.</summary>
        public static CompanionFollower Active { get; private set; }

        [Tooltip("Combat roster entry used when this companion joins an encounter.")]
        [SerializeField] private string combatUnit = "Guard";
        [SerializeField] private float followDistance = 1.4f;
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float catchUpDistance = 9f;
        [Tooltip("Seconds the player can stay on another floor before the companion catches up.")]
        [SerializeField] private float floorChangeDelay = 1f;
        [Tooltip("Pushes the companion slightly behind the player's lane so they don't overlap on screen.")]
        [SerializeField] private float depthOffset = 0.6f;
        [SerializeField] private LayerMask ground = ~0;

        private Transform _target;
        private PlayerMotor _motor;
        private float _offFloorTime;
        private float _fallSpeed;
        private int _facing = -1;

        public string CombatUnit => combatUnit;
        public bool Paused { get; private set; }
        public bool IsFollowing => _target != null && !Paused;

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public void Begin(PlayerInteractor player)
        {
            _target = player.transform;
            _motor = player.Motor;
            Active = this;
            Paused = false;
            enabled = true;
        }

        /// <summary>Hands the transform to another system (combat) or takes it back.</summary>
        public void SetPaused(bool paused)
        {
            Paused = paused;
            _fallSpeed = 0f;
        }

        /// <summary>Puts the companion just behind the player, on the player's floor.</summary>
        public void WarpBehindPlayer()
        {
            if (_target == null) return;
            int facing = _motor != null ? _motor.Facing : 1;
            Vector3 t = _target.position;
            float x = t.x - facing * followDistance;
            float y = GroundBelow(new Vector3(x, t.y + 0.6f, 0f), out float gy) && Mathf.Abs(gy - t.y) < 0.5f ? gy : t.y;
            if (Mathf.Abs(y - t.y) >= 0.5f) x = t.x;
            transform.position = new Vector3(x, y, LaneZ() + depthOffset);
            Face(facing);
            _offFloorTime = 0f;
            _fallSpeed = 0f;
        }

        private void Update()
        {
            if (_target == null || Paused) return;
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 t = _target.position;
            int facing = _motor != null ? _motor.Facing : 1;

            // Horizontal: settle a step behind the player.
            float desiredX = t.x - facing * followDistance;
            float dx = desiredX - p.x;
            float speed = Mathf.Abs(t.x - p.x) > 4f ? runSpeed : walkSpeed;
            if (Mathf.Abs(dx) > 0.08f)
            {
                p.x += Mathf.Clamp(dx, -speed * dt, speed * dt);
                Face(dx > 0f ? 1 : -1);
            }
            else
            {
                Face(t.x >= p.x ? 1 : -1);
            }

            // Vertical: stick to the floor, fall off edges.
            if (GroundBelow(new Vector3(p.x, p.y + 0.6f, 0f), out float gy))
            {
                if (gy >= p.y - 0.05f)
                {
                    p.y = gy;
                    _fallSpeed = 0f;
                }
                else
                {
                    _fallSpeed += 30f * dt;
                    p.y = Mathf.Max(gy, p.y - _fallSpeed * dt);
                }
            }
            p.z = LaneZ() + depthOffset;
            transform.position = p;

            // Catch up across floors or long distances.
            bool playerSettled = _motor == null || _motor.State == MotorState.Grounded;
            _offFloorTime = playerSettled && Mathf.Abs(t.y - p.y) > 0.6f ? _offFloorTime + dt : 0f;
            if (_offFloorTime > floorChangeDelay || (playerSettled && Mathf.Abs(t.x - p.x) > catchUpDistance))
            {
                WarpBehindPlayer();
            }
        }

        private float LaneZ() => _motor != null ? _motor.LaneZ : 0f;

        private void Face(int dir)
        {
            if (dir == _facing) return;
            _facing = dir;
            transform.rotation = Quaternion.Euler(0f, dir > 0 ? 90f : -90f, 0f);
        }

        private bool GroundBelow(Vector3 origin, out float y)
        {
            y = 0f;
            float best = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, 12f, ground, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is CharacterController || h.normal.y < 0.5f) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    y = h.point.y;
                }
            }
            return best < float.MaxValue;
        }
    }
}
