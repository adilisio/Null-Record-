using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Failing-fluorescent flicker: mostly steady with a little hum, then occasional bursts of drop-outs.
    /// Drives a light's intensity and, optionally, an emissive fixture so they flicker together.
    /// </summary>
    public class LightFlicker : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light target;
        [SerializeField] private Renderer fixture;
        [SerializeField] private Color fixtureEmission = Color.white * 3f;
        [Tooltip("Seconds between bursts (random within the range).")]
        [SerializeField] private Vector2 burstInterval = new Vector2(2.5f, 6f);
        [SerializeField] private float burstLength = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float hum = 0.04f;

        private float _base;
        private float _nextBurst;
        private float _burstEnd;
        private float _seed;
        private MaterialPropertyBlock _block;

        public void Configure(Light light, Renderer emissive, Color emission)
        {
            target = light;
            fixture = emissive;
            fixtureEmission = emission;
        }

        private void Start()
        {
            if (target == null) target = GetComponent<Light>();
            _base = target != null ? target.intensity : 1f;
            _seed = transform.position.x * 13.7f;
            _nextBurst = Time.time + Random.Range(burstInterval.x, burstInterval.y);
        }

        private void Update()
        {
            float t = Time.time;
            float k = 1f - hum + Mathf.PerlinNoise(_seed, t * 8f) * hum;

            if (t >= _nextBurst)
            {
                _burstEnd = t + burstLength;
                _nextBurst = t + Random.Range(burstInterval.x, burstInterval.y);
            }
            if (t < _burstEnd)
            {
                // Hard on/off stutter during a burst.
                float n = Mathf.PerlinNoise(_seed + 7f, t * 40f);
                k *= n > 0.55f ? 1f : n > 0.35f ? 0.25f : 0.05f;
            }

            if (target != null) target.intensity = _base * k;
            if (fixture != null)
            {
                _block ??= new MaterialPropertyBlock();
                fixture.GetPropertyBlock(_block);
                _block.SetColor(EmissionColor, fixtureEmission * k);
                fixture.SetPropertyBlock(_block);
            }
        }
    }
}
