using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public enum Team { Player, Enemy }

    public enum HitResult { Missed, Hit, Blocked, Ignored }

    public struct HitInfo
    {
        public int Damage;
        /// <summary>Knockback velocity for the target (x, y).</summary>
        public Vector2 Knock;
        /// <summary>Where the blow came from (attacker position), for armour and knockback direction.</summary>
        public Vector3 From;
        public Combatant Source;
    }

    /// <summary>
    /// Anything that can be hit in real-time combat: HP, a body box on the side-on plane (feet at the transform),
    /// brief invulnerability after a hit, and an optional block rule (the Warden's armour).
    /// Hits are plain box overlaps on X/Y, so no physics layers or colliders are involved.
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        private static readonly List<Combatant> All = new List<Combatant>();
        public static IReadOnlyList<Combatant> Registered => All;

        [SerializeField] private Team team = Team.Enemy;
        [SerializeField] private int maxHp = 3;
        [SerializeField] private float width = 0.7f;
        [SerializeField] private float height = 1.7f;
        [SerializeField] private float invulnerableTime = 0.25f;

        public Team Team => team;
        public int MaxHp => maxHp;
        public int Hp { get; private set; }
        public bool Dead => Hp <= 0;
        public float Height { get => height; set => height = value; }
        public float Width => width;
        public bool Invulnerable => Time.time < _invulnerableUntil;

        /// <summary>Returns true when this hit is blocked (no damage).</summary>
        public Func<HitInfo, bool> Blocks;
        public event Action<HitInfo> Hurt;
        public event Action<HitInfo> Blocked;
        public event Action Died;

        private float _invulnerableUntil;
        private bool _initialised;

        public void Configure(Team t, int hp, float w, float h, float iframes)
        {
            team = t;
            maxHp = hp;
            width = w;
            height = h;
            invulnerableTime = iframes;
            Hp = hp;
            _initialised = true;
        }

        public Rect Body
        {
            get
            {
                Vector3 p = transform.position;
                return new Rect(p.x - width * 0.5f, p.y, width, height);
            }
        }

        private void Awake()
        {
            if (!_initialised) Hp = maxHp;
            _initialised = true;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void SetMaxHp(int hp, bool refill)
        {
            maxHp = Mathf.Max(1, hp);
            Hp = refill ? maxHp : Mathf.Min(Hp, maxHp);
        }

        public void Refill()
        {
            Hp = maxHp;
            _invulnerableUntil = 0f;
        }

        public void Heal(int amount) => Hp = Mathf.Min(maxHp, Hp + amount);

        /// <summary>Invulnerable for a while (after a hit, or while respawning).</summary>
        public void MakeInvulnerable(float seconds) => _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        public HitResult Damage(HitInfo hit)
        {
            if (Dead || Invulnerable || !isActiveAndEnabled) return HitResult.Ignored;
            if (Blocks != null && Blocks(hit))
            {
                Blocked?.Invoke(hit);
                return HitResult.Blocked;
            }
            Hp = Mathf.Max(0, Hp - hit.Damage);
            _invulnerableUntil = Time.time + invulnerableTime;
            Hurt?.Invoke(hit);
            if (Hp <= 0) Died?.Invoke();
            return HitResult.Hit;
        }

        /// <summary>
        /// Hits every living combatant of the other team overlapping <paramref name="box"/>, skipping those in
        /// <paramref name="already"/> (one hit per swing). Returns the best result (Hit over Blocked over the rest).
        /// </summary>
        public static HitResult HitBox(Rect box, Team attacker, HitInfo hit, HashSet<Combatant> already = null)
        {
            HitResult best = HitResult.Missed;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                Combatant c = All[i];
                if (c.team == attacker || c.Dead || (already != null && already.Contains(c))) continue;
                if (!box.Overlaps(c.Body)) continue;
                HitResult r = c.Damage(hit);
                if (r != HitResult.Ignored) already?.Add(c);
                if (r == HitResult.Hit) best = HitResult.Hit;
                else if (r == HitResult.Blocked && best != HitResult.Hit) best = HitResult.Blocked;
                else if (best == HitResult.Missed) best = r;
            }
            return best;
        }

        /// <summary>A box in front of <paramref name="feet"/>: from <paramref name="near"/> to <paramref name="far"/> metres ahead, y measured from the feet.</summary>
        public static Rect Front(Vector3 feet, int facing, float near, float far, float yMin, float yMax)
        {
            float x0 = facing > 0 ? feet.x + near : feet.x - far;
            return new Rect(x0, feet.y + yMin, far - near, yMax - yMin);
        }
    }
}
