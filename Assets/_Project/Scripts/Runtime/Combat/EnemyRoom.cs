using System.Collections.Generic;
using Game.Audio;
using Game.Companions;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A room with hostiles in it (Security, Server Hall, Specimen Hall). The fight starts when you walk in; there's
    /// no lock, so you can also run through. Beating every hostile clears the room for good: scrap, XP, a log
    /// fragment and the <c>encounter_*_cleared</c> flag. Dying resets the room, and so does leaving it for a few
    /// seconds (the survivors heal and go back to their posts). A companion pitches in while you fight.
    /// </summary>
    public class EnemyRoom : MonoBehaviour
    {
        /// <summary>The room you're fighting in right now, if any.</summary>
        public static EnemyRoom Engaged { get; private set; }

        public const float LeaveResetSeconds = 2.5f;

        [SerializeField] private Rect area;
        [SerializeField] private Enemy[] enemies = new Enemy[0];
        [SerializeField] private int rewardScrap = 30;
        [SerializeField] private int rewardXp = 50;
        [SerializeField] private string clearedFlag;
        [SerializeField] private string rewardLog;
        [SerializeField] private Renderer marker;
        [SerializeField] private GameObject[] hideWhenCleared = new GameObject[0];

        private PlayerInventory _inv;
        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private bool _engaged;
        private float _outside;
        private float _assistAt;

        public Rect Area => area;
        public string ClearedFlag => clearedFlag;
        public int RewardScrap => rewardScrap;
        public int RewardXp => rewardXp;
        public bool PlayerInside { get; private set; }
        public bool Cleared { get; private set; }
        public IReadOnlyList<Enemy> Enemies => enemies;

        public int Alive
        {
            get
            {
                int n = 0;
                foreach (Enemy e in enemies) if (e != null && e.State != Enemy.Phase.Dead) n++;
                return n;
            }
        }

        public void Configure(Rect newArea, Enemy[] hostiles, int scrap, int xp, string flag, string log, Renderer zoneMarker, params GameObject[] hide)
        {
            area = newArea;
            enemies = hostiles;
            rewardScrap = scrap;
            rewardXp = xp;
            clearedFlag = flag;
            rewardLog = log;
            marker = zoneMarker;
            hideWhenCleared = hide;
        }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _inv = p.GetComponent<PlayerInventory>();
                _motor = p.GetComponent<PlayerMotor>();
                _combat = p.GetComponent<PlayerCombat>();
            }
            foreach (Enemy e in enemies) if (e != null) e.Attach(this);
            if (_inv != null && !string.IsNullOrEmpty(clearedFlag) && _inv.HasFlag(clearedFlag)) MarkCleared(silent: true);
        }

        private void OnEnable() => PlayerRespawn.Respawned += OnRespawned;

        private void OnDisable()
        {
            PlayerRespawn.Respawned -= OnRespawned;
            if (Engaged == this) SetEngaged(false);
        }

        private void OnRespawned()
        {
            if (!Cleared) ResetRoom();
        }

        /// <summary>Every hostile back at its post with full HP (the dead get back up too).</summary>
        public void ResetRoom()
        {
            if (Cleared) return;
            foreach (Enemy e in enemies) if (e != null) e.ResetToSpawn();
            foreach (Bolt b in new List<Bolt>(Bolt.Live)) Destroy(b.gameObject);
            SetEngaged(false);
        }

        private void Update()
        {
            if (_motor == null) return;
            PlayerInside = area.Contains((Vector2)(_motor.transform.position + Vector3.up * 0.5f));
            if (Cleared) return;

            if (Alive == 0)
            {
                MarkCleared(silent: false);
                return;
            }

            SetEngaged(PlayerInside && !Enemy.Passive);

            if (!PlayerInside)
            {
                _outside += Time.deltaTime;
                if (_outside >= LeaveResetSeconds && NeedsReset()) ResetLiving();
            }
            else _outside = 0f;

            if (_engaged) Assist();
        }

        private bool NeedsReset()
        {
            foreach (Enemy e in enemies)
            {
                if (e == null || e.State == Enemy.Phase.Dead) continue;
                if (e.Body.Hp < e.Body.MaxHp || (e.transform.position - e.Spawn).sqrMagnitude > 0.01f) return true;
            }
            return false;
        }

        private void ResetLiving()
        {
            foreach (Enemy e in enemies) if (e != null && e.State != Enemy.Phase.Dead) e.ResetToSpawn();
        }

        private void SetEngaged(bool on)
        {
            if (on == _engaged) return;
            _engaged = on;
            if (on)
            {
                Engaged = this;
                GameAudio.SetCombat(true);
                _assistAt = Time.time + 1.2f;
            }
            else if (Engaged == this)
            {
                Engaged = null;
                GameAudio.SetCombat(false);
            }
        }

        private void MarkCleared(bool silent)
        {
            Cleared = true;
            SetEngaged(false);
            foreach (Enemy e in enemies) if (e != null && (silent || e.State != Enemy.Phase.Dead)) e.Remove();
            foreach (GameObject g in hideWhenCleared) if (g != null) g.SetActive(false);
            if (marker != null) CombatVisuals.Tint(marker, new Color(0.15f, 0.15f, 0.17f), emissive: true);
            if (silent || _inv == null) return;

            int levelBefore = Progression.Level(_inv.Xp);
            _inv.AddScrap(rewardScrap);
            _inv.AddXp(rewardXp);
            if (!string.IsNullOrEmpty(clearedFlag)) _inv.SetFlag(clearedFlag);
            GameAudio.Play(SoundId.Victory);
            GameMessages.Show($"Area cleared. +{rewardScrap} scrap, +{rewardXp} XP");
            if (Progression.Level(_inv.Xp) > levelBefore)
                GameMessages.Show($"Level {Progression.Level(_inv.Xp)}: max HP +{Progression.HpPerLevel}.");
            if (!string.IsNullOrEmpty(rewardLog)) GameMessages.Show(rewardLog);
        }

        /// <summary>
        /// The companion's help: Dace swings a baton at a hostile next to them; the Researcher patches you up
        /// (+1 HP) every so often while you're hurt. Simple on purpose; companion combat AI comes later.
        /// </summary>
        private void Assist()
        {
            CompanionFollower c = CompanionFollower.Active;
            if (c == null || !c.IsFollowing || Time.time < _assistAt) return;
            var anim = c.GetComponentInChildren<Game.World.CharacterAnimator>();
            if (c.CombatUnit == "Researcher")
            {
                _assistAt = Time.time + 1f;
                if (_combat == null || _combat.Hp >= _combat.MaxHp || _combat.Hp <= 0) return;
                _combat.Body.Heal(1);
                anim?.Play("Shove", 0.4f);
                GameAudio.Play(SoundId.Pickup, _motor.transform.position + Vector3.up, 0.6f);
                GameMessages.Show("Patched up (+1 HP).");
                _assistAt = Time.time + 7f;
                return;
            }

            _assistAt = Time.time + 0.25f;
            Vector3 me = c.transform.position;
            foreach (Enemy e in enemies)
            {
                if (e == null || e.State == Enemy.Phase.Dead || e.Kind == EnemyKind.Sentry && Mathf.Abs(e.transform.position.y - me.y) > 1f) continue;
                float dx = e.transform.position.x - me.x;
                if (Mathf.Abs(dx) > 1.9f || Mathf.Abs(e.transform.position.y - me.y) > 1.2f) continue;
                c.transform.rotation = Quaternion.Euler(0f, dx > 0f ? 90f : -90f, 0f);
                anim?.Play("Swing", 0.4f);
                GameAudio.Play(SoundId.Swing, me + Vector3.up, 0.7f);
                e.Body.Damage(new HitInfo { Damage = 1, Knock = new Vector2(Mathf.Sign(dx) * 3f, 2f), From = me, Source = null });
                _assistAt = Time.time + 1.6f;
                return;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
