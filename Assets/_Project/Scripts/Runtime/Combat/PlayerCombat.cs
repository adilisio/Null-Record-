using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Dialogue;
using Game.Player;
using Game.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// The Subject's side of real-time combat: a strike with fists or a found weapon (on the ground, mid-air, or low while crouched),
    /// HP, knockback with a moment of invulnerability when hit, and death sending you back to the last checkpoint.
    /// HP refills at checkpoints and on respawn, and isn't saved.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerCombat : MonoBehaviour
    {
        public const int BaseHp = 5;
        public const float HurtInvulnerability = 1.0f;

        private PlayerMotor _motor;
        private PlayerInputReader _input;
        private PlayerInventory _inv;
        private PlayerRespawn _respawn;
        private Combatant _body;
        private CharacterAnimator _anim;
        private Renderer[] _renderers;
        private InputAction _attack, _next, _prev;
        private readonly List<WeaponDef> _owned = new List<WeaponDef> { Weapons.Fists };
        private Transform[] _props = new Transform[0];

        private float _swingStart = -10f;
        private bool _swinging;
        private int _swingFacing = 1;
        private bool _swingLow;
        private bool _swingAir;
        private readonly HashSet<Combatant> _hitThisSwing = new HashSet<Combatant>();
        private Vector3 _lastCheckpoint;
        private int _lastLevel;
        private bool _blockedHintShown;

        public Combatant Body => _body;
        public int Hp => _body != null ? _body.Hp : 0;
        public int MaxHp => _body != null ? _body.MaxHp : BaseHp;
        public bool Attacking => _swinging;
        /// <summary>Swings started (tests).</summary>
        public int SwingCount { get; private set; }
        /// <summary>Result of the last swing that connected with something.</summary>
        public HitResult LastResult { get; private set; } = HitResult.Missed;
        /// <summary>The weapon in hand.</summary>
        public WeaponDef Weapon { get; private set; } = Weapons.Fists;
        /// <summary>Weapons found so far, fists first.</summary>
        public IReadOnlyList<WeaponDef> Owned => _owned;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInputReader>();
            _inv = GetComponent<PlayerInventory>();
            _respawn = GetComponent<PlayerRespawn>();
            _body = GetComponent<Combatant>();
            if (_body == null) _body = gameObject.AddComponent<Combatant>();
            _body.Configure(Team.Player, BaseHp, 0.6f, 1.75f, HurtInvulnerability);
            _body.Hurt += OnHurt;
            _body.Died += OnDied;
            _anim = GetComponentInChildren<CharacterAnimator>();
            // Only renderers that are visible to begin with flicker (hidden placeholder bodies stay hidden).
            _renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.enabled);
            var props = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Weapon_")) props.Add(t);
            _props = props.ToArray();
            PlayerRespawn.Respawned += OnRespawned;
        }

        private void OnDestroy() => PlayerRespawn.Respawned -= OnRespawned;

        private void Start()
        {
            if (_input != null && _input.Map != null)
            {
                _attack = _input.Map.FindAction("Attack");
                _next = _input.Map.FindAction("Next");
                _prev = _input.Map.FindAction("Previous");
            }
            RefreshOwned(announce: false);
            _lastLevel = _inv != null ? Progression.Level(_inv.Xp) : 1;
            _body.SetMaxHp(MaxHpFor(_inv != null ? _inv.Xp : 0), true);
            if (_respawn != null) _lastCheckpoint = _respawn.Checkpoint;
        }

        public static int MaxHpFor(int xp) => BaseHp + Progression.BonusHp(xp);

        private bool CanAct =>
            _motor.ControlEnabled && !_motor.ExternallyControlled && !_motor.Stunned && !_body.Dead && Time.timeScale > 0f
            && _motor.State != MotorState.Ladder && _motor.State != MotorState.LedgeHang && _motor.State != MotorState.LedgeClimb
            && (DialogueUI.Instance == null || !DialogueUI.Instance.IsOpen);

        private void Update()
        {
            // Level-ups raise max HP (and fill the new pip); checkpoints refill.
            int level = _inv != null ? Progression.Level(_inv.Xp) : 1;
            if (level != _lastLevel)
            {
                _lastLevel = level;
                _body.SetMaxHp(MaxHpFor(_inv.Xp), false);
                _body.Heal(_body.MaxHp);
            }
            if (_respawn != null && _respawn.Checkpoint != _lastCheckpoint)
            {
                _lastCheckpoint = _respawn.Checkpoint;
                _body.Refill();
            }

            RefreshOwned(announce: true);
            if (CanAct && _next != null && _next.WasPressedThisFrame()) Cycle(1);
            if (CanAct && _prev != null && _prev.WasPressedThisFrame()) Cycle(-1);
            if (_attack != null && _attack.WasPressedThisFrame() && FromAllowedDevice()) TryAttack();
            TickSwing();
            Flicker();
            // Crouching shrinks the body, so a crouch slips under high shots.
            _body.Height = _motor.IsCrouching ? 1.0f : 1.75f;
        }

        /// <summary>
        /// On touch screens every tap also arrives as a mouse click, so only the on-screen ATTACK button (a virtual
        /// gamepad) attacks there. Enter is bound to Attack for menus and dialogue; it doesn't swing.
        /// </summary>
        private bool FromAllowedDevice()
        {
            InputDevice d = _attack.activeControl != null ? _attack.activeControl.device : null;
            if (d is Keyboard && _attack.activeControl.name == "enter") return false;
            if (PlayerInputReader.TouchStyle && (d is Mouse || d is Pointer)) return false;
            return true;
        }

        /// <summary>Starts a strike if nothing prevents it. Returns whether it started.</summary>
        public bool TryAttack()
        {
            if (!CanAct || _swinging) return false;
            _swinging = true;
            _swingStart = Time.time;
            _swingFacing = _motor.Facing;
            _swingLow = _motor.IsCrouching;
            _swingAir = _motor.State == MotorState.Airborne;
            _hitThisSwing.Clear();
            _motor.AttackLocked = true;
            SwingCount++;
            _anim?.Play(Weapon.Anim, Weapon.Total);
            GameAudio.Play(SoundId.Swing, transform.position + Vector3.up, 0.8f);
            return true;
        }

        /// <summary>The strike's hit box right now (also for tests and gizmos).</summary>
        public Rect StrikeBox => _swingLow
            ? Combatant.Front(transform.position, _swingFacing, 0.1f, Weapon.Reach, 0.1f, 1.0f)
            : _swingAir
                ? Combatant.Front(transform.position, _swingFacing, 0.1f, Weapon.Reach, -0.4f, 1.65f) // reaches down a little from a jump
                : Combatant.Front(transform.position, _swingFacing, 0.1f, Weapon.Reach, 0.55f, 1.65f);

        private void TickSwing()
        {
            if (!_swinging) return;
            float t = Time.time - _swingStart;
            WeaponDef w = Weapon;
            if (t >= w.Startup && t <= w.Startup + w.Active)
            {
                Rect box = StrikeBox;
                var hit = new HitInfo
                {
                    Damage = w.Damage, Knock = new Vector2(_swingFacing * (w.Damage > 1 ? 5f : 3.5f), 2f), From = transform.position, Source = _body,
                    Stun = w.Stun, Pierce = w.Stun > 0f,
                };
                HitResult r = Combatant.HitBox(box, Team.Player, hit, _hitThisSwing);
                if (r == HitResult.Hit)
                {
                    LastResult = r;
                    GameAudio.Play(SoundId.Hit, transform.position + new Vector3(_swingFacing, 1.1f, 0f));
                    if (w.Stun > 0f) GameAudio.Play(SoundId.Zap, transform.position + new Vector3(_swingFacing, 1.1f, 0f), 0.6f);
                }
                else if (r == HitResult.Blocked)
                {
                    LastResult = r;
                    GameAudio.Play(SoundId.Knockback, transform.position + new Vector3(_swingFacing, 1.1f, 0f), 0.7f);
                    _motor.Knockback(new Vector2(-_swingFacing * 4f, 1.5f), 0.2f);
                    if (!_blockedHintShown)
                    {
                        _blockedHintShown = true;
                        GameMessages.Show("Its armour turns the blow. Strike right after it swings, from behind, or with something that shocks.");
                    }
                }
                for (int i = Bolt.Live.Count - 1; i >= 0; i--)
                {
                    if (box.Overlaps(Bolt.Live[i].Box)) Bolt.Live[i].Pop();
                }
            }
            if (t > w.Total || !_motor.ControlEnabled || _motor.Stunned)
            {
                _swinging = false;
                _motor.AttackLocked = false;
            }
        }

        /// <summary>Picks up weapons granted to the inventory (pickups, loading a save); a new find is equipped.</summary>
        private void RefreshOwned(bool announce)
        {
            if (_inv == null) return;
            WeaponDef newest = null;
            foreach (WeaponDef w in Weapons.All)
            {
                if (w == Weapons.Fists || _owned.Contains(w) || !_inv.HasTool(w.Id)) continue;
                _owned.Add(w);
                newest = w;
            }
            if (newest == null) return;
            _owned.Sort((a, b) => System.Array.IndexOf(Weapons.All, a).CompareTo(System.Array.IndexOf(Weapons.All, b)));
            Equip(newest);
            if (announce)
            {
                string how = PlayerInputReader.TouchStyle ? "Tap the weapon button" : "Press 1 / 2";
                GameMessages.Show($"Equipped: {newest.Name}. {how} to switch.");
            }
        }

        /// <summary>Next (+1) or previous (-1) of the weapons found so far.</summary>
        public void Cycle(int dir)
        {
            if (_owned.Count < 2 || _swinging) return;
            int i = (_owned.IndexOf(Weapon) + dir + _owned.Count) % _owned.Count;
            Equip(_owned[i]);
            GameAudio.Play(SoundId.UiMove, transform.position + Vector3.up, 0.6f);
        }

        public void Equip(WeaponDef w)
        {
            if (w == null || !_owned.Contains(w)) return;
            Weapon = w;
            foreach (Transform p in _props) if (p != null) p.gameObject.SetActive(p.name == w.Prop);
        }

        private void OnHurt(HitInfo hit)
        {
            float dir = Mathf.Sign(transform.position.x - hit.From.x);
            if (dir == 0f) dir = -_motor.Facing;
            _swinging = false;
            _motor.AttackLocked = false;
            _motor.Knockback(new Vector2(dir * Mathf.Max(4f, Mathf.Abs(hit.Knock.x)), Mathf.Max(3.5f, hit.Knock.y)), 0.35f);
            _anim?.Play("Hit", 0.3f);
            GameAudio.Play(SoundId.Hurt, transform.position + Vector3.up);
        }

        private void OnDied()
        {
            _respawn?.Kill("Overpowered.");
        }

        private void OnRespawned()
        {
            _body.Refill();
            _body.MakeInvulnerable(1.0f);
        }

        private void Flicker()
        {
            bool hide = _body.Invulnerable && !_body.Dead && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            foreach (Renderer r in _renderers)
            {
                if (r != null && r.enabled == hide) r.enabled = !hide;
            }
        }
    }
}
