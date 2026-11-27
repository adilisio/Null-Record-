using Game.Audio;
using Game.Player;
using Game.World;
using UnityEngine;

namespace Game.Combat
{
    public enum EnemyKind { Enforcer, Sentry, Warden }

    /// <summary>
    /// A real-time hostile. Every attack is telegraphed: a red "!" and a pause (the wind-up), then the attack, then a
    /// recovery where it's open. Kinds:
    /// <list type="bullet">
    /// <item>Enforcer: walks up and lunges with a stun prod. A hit interrupts it.</item>
    /// <item>Sentry: holds position and fires slow taser bolts (strike them out of the air, or crouch under high ones).</item>
    /// <item>Warden: slow and heavy; its armour blocks hits from the front except while it recovers from a swing.
    /// Knockback can't move it, but shock grates hurt it.</item>
    /// </list>
    /// Enemies stay inside their room, never walk off ledges, and freeze while the player isn't in control
    /// (dialogue, lifts, the end card) or when <see cref="Passive"/> is set (tests that only walk the route).
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class Enemy : MonoBehaviour
    {
        /// <summary>Tests that walk the route turn hostiles off; combat tests turn them back on.</summary>
        public static bool Passive { get; set; }

        public enum Phase { Idle, Chase, Windup, Attack, Recover, Flinch, Dead }

        [SerializeField] private EnemyKind kind = EnemyKind.Enforcer;
        [SerializeField] private Transform model;
        [SerializeField] private TextMesh alert;

        private Combatant _body;
        private CharacterAnimator _anim;
        private EnemyRoom _room;
        private Vector3 _spawn;
        private int _facing = -1;
        private float _phaseUntil;
        private float _vy;
        private float _knockVx;
        private readonly System.Collections.Generic.HashSet<Combatant> _hit = new System.Collections.Generic.HashSet<Combatant>();
        private float _deadAt;
        private int _spawnFacing = -1;
        private PlayerMotor _player;
        private Combatant _playerBody;

        public EnemyKind Kind => kind;
        public Phase State { get; private set; } = Phase.Idle;
        public Combatant Body => _body;
        public bool Dead => _body != null && _body.Dead;
        public int Facing => _facing;
        public Vector3 Spawn => _spawn;

        // ---------------------------------------------------------------- tuning (first pass)
        private float Speed => kind == EnemyKind.Warden ? 1.3f : kind == EnemyKind.Enforcer ? 2.4f : 0f;
        private float Sight => kind == EnemyKind.Sentry ? 10f : 8f;
        private float AttackRange => kind == EnemyKind.Warden ? 1.9f : kind == EnemyKind.Enforcer ? 1.35f : 10f;
        public float WindupTime => kind == EnemyKind.Warden ? 0.85f : kind == EnemyKind.Enforcer ? 0.45f : 0.7f;
        private float AttackTime => kind == EnemyKind.Warden ? 0.25f : kind == EnemyKind.Enforcer ? 0.2f : 0.05f;
        public float RecoverTime => kind == EnemyKind.Warden ? 1.1f : kind == EnemyKind.Enforcer ? 0.65f : 1.5f;
        private int AttackDamage => kind == EnemyKind.Warden ? 2 : 1;
        public static int HpFor(EnemyKind k) => k == EnemyKind.Warden ? 8 : k == EnemyKind.Enforcer ? 3 : 2;

        public void Configure(EnemyKind k, Transform modelRoot, TextMesh alertText)
        {
            kind = k;
            model = modelRoot;
            alert = alertText;
        }

        public void Attach(EnemyRoom room) => _room = room;

        private void Awake()
        {
            _body = GetComponent<Combatant>();
            float w = kind == EnemyKind.Warden ? 0.95f : 0.7f;
            float h = kind == EnemyKind.Warden ? 2.0f : 1.75f;
            _body.Configure(Team.Enemy, HpFor(kind), w, h, 0.2f);
            _body.Hurt += OnHurt;
            _body.Died += OnDied;
            _body.Blocked += OnBlocked;
            if (kind == EnemyKind.Warden) _body.Blocks = ArmourBlocks;
            _anim = GetComponentInChildren<CharacterAnimator>();
            _spawn = transform.position;
            _spawnFacing = model != null && Vector3.Dot(model.forward, Vector3.right) > 0f ? 1 : -1;
            _facing = _spawnFacing;
            if (alert != null) alert.gameObject.SetActive(false);
        }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _player = p.GetComponent<PlayerMotor>();
                _playerBody = p.GetComponent<Combatant>();
            }
        }

        /// <summary>Back to the spawn point, full HP, standing (room reset after a death or leaving the room).</summary>
        public void ResetToSpawn()
        {
            transform.position = _spawn;
            _facing = _spawnFacing;
            _vy = 0f;
            _knockVx = 0f;
            _body.Refill();
            gameObject.SetActive(true);
            if (model != null)
            {
                model.gameObject.SetActive(true);
                model.localRotation = Quaternion.Euler(0f, _facing > 0 ? 90f : -90f, 0f);
                model.localPosition = Vector3.zero;
            }
            _anim?.ResetPose();
            SetPhase(Phase.Idle, 0f);
        }

        /// <summary>Gone for good (room cleared earlier, e.g. after loading a save).</summary>
        public void Remove()
        {
            State = Phase.Dead;
            gameObject.SetActive(false);
        }

        private bool Frozen =>
            Passive || _player == null || _room == null || !_room.PlayerInside
            || !_player.ControlEnabled || _player.ExternallyControlled || Time.timeScale <= 0f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (State == Phase.Dead)
            {
                TickDeath();
                return;
            }
            Gravity(dt);
            if (Frozen)
            {
                if (alert != null && alert.gameObject.activeSelf && (State != Phase.Windup || Passive)) alert.gameObject.SetActive(false);
                return;
            }

            Vector3 me = transform.position;
            Vector3 target = _player.transform.position;
            float dx = target.x - me.x;
            float dy = target.y - me.y;
            bool sees = Mathf.Abs(dx) <= Sight && Mathf.Abs(dy) < (kind == EnemyKind.Sentry ? 6f : 2.5f);

            switch (State)
            {
                case Phase.Idle:
                case Phase.Chase:
                    if (!sees)
                    {
                        State = Phase.Idle;
                        break;
                    }
                    Face(dx);
                    if (Mathf.Abs(dx) <= AttackRange && (kind == EnemyKind.Sentry || Mathf.Abs(dy) < 1.4f))
                    {
                        SetPhase(Phase.Windup, WindupTime);
                        break;
                    }
                    State = Phase.Chase;
                    Walk(Mathf.Sign(dx) * Speed * dt);
                    break;
                case Phase.Windup:
                    if (kind == EnemyKind.Sentry) Face(dx); // the turret keeps tracking
                    if (Time.time >= _phaseUntil) BeginAttack(target);
                    break;
                case Phase.Attack:
                    if (kind == EnemyKind.Enforcer) Walk(_facing * 3.2f * dt); // lunge
                    if (kind != EnemyKind.Sentry) StrikeBox();
                    if (Time.time >= _phaseUntil) SetPhase(Phase.Recover, RecoverTime);
                    break;
                case Phase.Recover:
                case Phase.Flinch:
                    if (Time.time >= _phaseUntil) SetPhase(Phase.Chase, 0f);
                    break;
            }

            if (Mathf.Abs(_knockVx) > 0.01f)
            {
                Walk(_knockVx * dt);
                _knockVx = Mathf.MoveTowards(_knockVx, 0f, 12f * dt);
            }
            ContactDamage();
        }

        private void SetPhase(Phase p, float duration)
        {
            State = p;
            _phaseUntil = Time.time + duration;
            if (alert != null) alert.gameObject.SetActive(p == Phase.Windup);
        }

        private void BeginAttack(Vector3 target)
        {
            SetPhase(Phase.Attack, AttackTime);
            _hit.Clear();
            if (kind == EnemyKind.Sentry)
            {
                _anim?.Play("Shoot", 0.4f);
                Vector3 from = transform.position + new Vector3(_facing * 0.5f, 1.35f, 0f);
                Vector3 aim = target + Vector3.up * 1.1f - from;
                aim.z = 0f;
                // Mostly level shots: steep angles are clamped so a crouch or a jump can still dodge.
                Vector3 dir = new Vector3(Mathf.Sign(aim.x), Mathf.Clamp(aim.y / Mathf.Max(1f, Mathf.Abs(aim.x)), -0.6f, 0.6f), 0f).normalized;
                Bolt.Fire(from, dir * 6.5f, 1);
                GameAudio.Play(SoundId.Zap, from, 0.8f);
            }
            else
            {
                _anim?.Play("Swing", AttackTime + 0.2f);
                GameAudio.Play(SoundId.Swing, transform.position + Vector3.up);
            }
        }

        private void StrikeBox()
        {
            Rect box = kind == EnemyKind.Warden
                ? Combatant.Front(transform.position, _facing, 0.1f, 2.1f, 0.0f, 2.0f)
                : Combatant.Front(transform.position, _facing, 0.1f, 1.4f, 0.4f, 1.6f);
            var hit = new HitInfo
            {
                Damage = AttackDamage,
                Knock = new Vector2(_facing * (kind == EnemyKind.Warden ? 7f : 4.5f), kind == EnemyKind.Warden ? 5f : 3.5f),
                From = transform.position,
                Source = _body,
            };
            Combatant.HitBox(box, Team.Enemy, hit, _hit);
        }

        /// <summary>Touching a walking enemy hurts (not the turret).</summary>
        private void ContactDamage()
        {
            if (kind == EnemyKind.Sentry || _playerBody == null || State == Phase.Flinch) return;
            Rect me = _body.Body;
            Rect shrunk = new Rect(me.x + 0.1f, me.y + 0.1f, me.width - 0.2f, me.height - 0.3f);
            if (!shrunk.Overlaps(_playerBody.Body)) return;
            _playerBody.Damage(new HitInfo { Damage = 1, Knock = new Vector2(Mathf.Sign(_playerBody.transform.position.x - transform.position.x) * 4f, 3.5f), From = transform.position, Source = _body });
        }

        private bool ArmourBlocks(HitInfo hit)
        {
            if (State == Phase.Recover || hit.Pierce) return false; // open right after a swing; shock gets through
            if (hit.Source == null) return false; // hazards and falls ignore armour
            float side = Mathf.Sign(hit.From.x - transform.position.x);
            return side == _facing; // from the front
        }

        private void OnBlocked(HitInfo hit) => Tint(new Color(0.75f, 0.8f, 0.9f));

        private void OnHurt(HitInfo hit)
        {
            Tint(hit.Stun > 0f ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.55f, 0.5f));
            if (kind == EnemyKind.Warden)
            {
                // Too heavy to flinch or be knocked back, but a shock locks it up.
                if (hit.Stun > 0f) SetPhase(Phase.Flinch, hit.Stun);
                return;
            }
            _knockVx = Mathf.Sign(transform.position.x - hit.From.x) * Mathf.Min(4f, Mathf.Abs(hit.Knock.x) + 1f);
            _anim?.Play("Hit", 0.25f);
            SetPhase(Phase.Flinch, Mathf.Max(0.3f, hit.Stun));
        }

        private void OnDied()
        {
            SetPhase(Phase.Dead, 0f);
            _deadAt = Time.time;
            _anim?.Play("Hit", 1f);
            GameAudio.Play(SoundId.UnitDown, transform.position + Vector3.up);
        }

        private void TickDeath()
        {
            if (model == null) return;
            float t = Time.time - _deadAt;
            // Topple backwards, then fade out of the way.
            float k = Mathf.Clamp01(t / 0.35f);
            model.localRotation = Quaternion.Euler(0f, _facing > 0 ? 90f : -90f, 0f) * Quaternion.Euler(-80f * k, 0f, 0f);
            if (t > 1.1f && model.gameObject.activeSelf) model.gameObject.SetActive(false);
        }

        private float _tintUntil;

        private void Tint(Color c)
        {
            _tintUntil = Time.time + 0.12f;
            if (model == null) return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) CombatVisuals.Tint(r, c, emissive: true);
        }

        private void LateUpdate()
        {
            if (_tintUntil > 0f && Time.time > _tintUntil)
            {
                _tintUntil = 0f;
                if (model != null) foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) CombatVisuals.ClearTint(r);
            }
        }

        private void Face(float dx)
        {
            if (Mathf.Abs(dx) < 0.05f) return;
            int f = dx > 0f ? 1 : -1;
            if (f == _facing) return;
            _facing = f;
            // Only the model turns, so the "!" above the head stays readable.
            if (model != null) model.localRotation = Quaternion.Euler(0f, f > 0 ? 90f : -90f, 0f);
        }

        /// <summary>Horizontal move that stays in the room, stops at walls and never steps off a ledge.</summary>
        private void Walk(float step)
        {
            if (Mathf.Abs(step) < 1e-5f) return;
            Vector3 p = transform.position;
            float nx = p.x + step;
            if (_room != null)
            {
                Rect a = _room.Area;
                nx = Mathf.Clamp(nx, a.xMin + 0.4f, a.xMax - 0.4f);
            }
            float dir = Mathf.Sign(step);
            float half = _body.Width * 0.5f;
            if (Physics.Raycast(new Vector3(p.x, p.y + 0.5f, 0f), Vector3.right * dir, out RaycastHit wall, half + Mathf.Abs(step), ~0, QueryTriggerInteraction.Ignore)
                && !(wall.collider is CharacterController))
            {
                // Enforcers hop knee-high obstacles (crates); the Warden is too heavy.
                if (kind == EnemyKind.Enforcer && _vy == 0f && !Physics.Raycast(new Vector3(p.x, p.y + 1.3f, 0f), Vector3.right * dir, half + 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    _vy = 7.5f;
                return; // wall or block (the player doesn't count: you can be walked into)
            }
            if (_vy == 0f && !Physics.Raycast(new Vector3(nx + dir * half, p.y + 0.3f, 0f), Vector3.down, kind == EnemyKind.Enforcer ? 1.4f : 0.7f, ~0, QueryTriggerInteraction.Ignore))
                return; // ledge
            p.x = nx;
            transform.position = p;
        }

        /// <summary>Falls when knocked off something; otherwise stays on the floor it stands on.</summary>
        private void Gravity(float dt)
        {
            Vector3 p = transform.position;
            if (_vy <= 0f && Physics.Raycast(new Vector3(p.x, p.y + 0.3f, 0f), Vector3.down, out RaycastHit hit, 0.35f + Mathf.Max(0f, -_vy * dt), ~0, QueryTriggerInteraction.Ignore)
                && !(hit.collider is CharacterController))
            {
                if (_vy < 0f || hit.point.y > p.y + 0.01f) p.y = hit.point.y;
                _vy = 0f;
            }
            else
            {
                _vy = Mathf.Max(_vy - 25f * dt, -20f);
                p.y += _vy * dt;
                if (p.y < _spawn.y - 30f) _body.Damage(new HitInfo { Damage = 99 }); // fell out of the world
            }
            transform.position = p;
        }
    }
}
