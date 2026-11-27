using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>A slow taser bolt from a Sentry. Hurts the player; a strike knocks it out of the air.</summary>
    public class Bolt : MonoBehaviour
    {
        private static readonly List<Bolt> All = new List<Bolt>();
        public static IReadOnlyList<Bolt> Live => All;

        private Vector3 _velocity;
        private float _dieAt;
        private int _damage;

        public static Bolt Fire(Vector3 from, Vector3 velocity, int damage)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bolt";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.3f;
            Renderer r = go.GetComponent<Renderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Color c = new Color(1f, 0.85f, 0.3f);
            if (unlit != null) r.material = new Material(unlit) { color = c };
            else r.material.color = c;
            var light = go.AddComponent<Light>();
            light.color = new Color(1f, 0.8f, 0.3f);
            light.range = 2.5f;
            light.intensity = 2f;
            Bolt b = go.AddComponent<Bolt>();
            b._velocity = velocity;
            b._dieAt = Time.time + 3f;
            b._damage = damage;
            return b;
        }

        public Rect Box
        {
            get
            {
                Vector3 p = transform.position;
                return new Rect(p.x - 0.18f, p.y - 0.18f, 0.36f, 0.36f);
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Knocked out of the air by a strike.</summary>
        public void Pop()
        {
            GameAudio.Play(SoundId.Zap, transform.position, 0.5f);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (Time.time > _dieAt)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 step = _velocity * Time.deltaTime;
            if (Physics.Raycast(transform.position, _velocity.normalized, out RaycastHit wall, step.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                && wall.collider.GetComponent<CharacterController>() == null)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            var hit = new HitInfo { Damage = _damage, Knock = new Vector2(Mathf.Sign(_velocity.x) * 4f, 3f), From = transform.position - _velocity.normalized };
            if (Combatant.HitBox(Box, Team.Enemy, hit) == HitResult.Hit) Destroy(gameObject);
        }
    }
}
