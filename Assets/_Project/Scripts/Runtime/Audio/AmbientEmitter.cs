using UnityEngine;

namespace Game.Audio
{
    /// <summary>A looping positional sound in the world (e.g. the steam vent).</summary>
    public class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] private SoundId sound = SoundId.SteamLoop;
        [SerializeField] private float maxDistance = 14f;

        private AudioSource _src;

        public SoundId Sound => sound;
        public AudioSource Source => _src;

        public void Configure(SoundId id, float range)
        {
            sound = id;
            maxDistance = range;
        }

        private void Start()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = AudioManager.Clip(sound);
            _src.loop = true;
            _src.spatialBlend = 0.85f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 2f;
            _src.maxDistance = maxDistance;
            _src.dopplerLevel = 0f;
            _src.time = Random.Range(0f, _src.clip.length);
            _src.Play();
        }

        private void Update()
        {
            if (_src != null) _src.volume = AudioManager.MixLevel(sound) * AudioManager.EffectsGain;
        }
    }
}
