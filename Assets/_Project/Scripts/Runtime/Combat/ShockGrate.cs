using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// An arcing floor grate, live only while its room's fight is on. Anyone standing on it takes 1 damage and is
    /// thrown up, enemies included, and the Warden's armour doesn't help. Luring hostiles onto it is the point.
    /// </summary>
    public class ShockGrate : MonoBehaviour
    {
        [SerializeField] private EnemyRoom room;
        [SerializeField] private float halfWidth = 0.45f;
        [SerializeField] private Renderer surface;

        private readonly Dictionary<Combatant, float> _next = new Dictionary<Combatant, float>();

        public bool Live => room != null && room == EnemyRoom.Engaged;

        public void Configure(EnemyRoom owner, float width, Renderer grateRenderer)
        {
            room = owner;
            halfWidth = width * 0.5f;
            surface = grateRenderer;
        }

        private void Update()
        {
            bool live = Live;
            if (surface != null)
            {
                float k = live ? 1.2f + Mathf.Sin(Time.time * 18f) * 0.8f : 0.15f;
                CombatVisuals.Tint(surface, new Color(0.7f, 1f, 0.15f) * k, emissive: true);
            }
            if (!live) return;

            Vector3 p = transform.position;
            IReadOnlyList<Combatant> all = Combatant.Registered;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Combatant c = all[i];
                if (c.Dead) continue;
                Vector3 f = c.transform.position;
                if (Mathf.Abs(f.x - p.x) > halfWidth + c.Width * 0.3f || Mathf.Abs(f.y - p.y) > 0.25f) continue;
                if (_next.TryGetValue(c, out float t) && Time.time < t) continue;
                _next[c] = Time.time + 0.8f;
                var hit = new HitInfo { Damage = 1, Knock = new Vector2(0f, 7f), From = f + Vector3.down, Source = null };
                if (c.Damage(hit) == HitResult.Hit) GameAudio.Play(SoundId.Zap, f + Vector3.up * 0.3f);
            }
        }
    }
}
