using System.Collections.Generic;
using Game.CameraSystem;
using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Plays sound effects from a small voice pool, crossfades a per-room ambience bed, and layers a combat
    /// loop during encounters. Sounds are synthesised once (see <see cref="Synth"/>) and cached for the session.
    /// Volumes: master (AudioListener) × effects or ambience (settings) × a per-sound mix level.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("Ambience when no room applies (e.g. the title screen). Leave empty for silence.")]
        [SerializeField] private string defaultRoom = "";
        [SerializeField] private float ambienceFade = 1.5f;
        [SerializeField] private int voices = 16;

        private static readonly Dictionary<SoundId, AudioClip[]> Library = new Dictionary<SoundId, AudioClip[]>();
        /// <summary>Loudness match for recorded clips (Resources/Sounds/gains.json, written by the editor's SoundTuning).</summary>
        private static readonly Dictionary<AudioClip, float> ClipGain = new Dictionary<AudioClip, float>();

        [System.Serializable] private class GainEntry { public string key; public float gain = 1f; }
        [System.Serializable] private class GainTable { public GainEntry[] entries; }

        /// <summary>Mix level per sound, relative to the effects/ambience volume.</summary>
        private static readonly Dictionary<SoundId, float> Mix = new Dictionary<SoundId, float>
        {
            { SoundId.Footstep, 0.32f }, { SoundId.Jump, 0.3f }, { SoundId.Land, 0.5f }, { SoundId.LadderClank, 0.28f },
            { SoundId.Hurt, 0.75f }, { SoundId.DoorOpen, 0.6f }, { SoundId.DoorDenied, 0.45f }, { SoundId.Pickup, 0.55f },
            { SoundId.SteamLoop, 0.5f }, { SoundId.UiMove, 0.25f }, { SoundId.UiConfirm, 0.4f }, { SoundId.DialogueBlip, 0.3f },
            { SoundId.Save, 0.2f }, { SoundId.CombatStep, 0.25f }, { SoundId.Swing, 0.45f }, { SoundId.Zap, 0.45f },
            { SoundId.Hit, 0.7f }, { SoundId.Knockback, 0.55f }, { SoundId.UnitDown, 0.6f }, { SoundId.PhasePlayer, 0.4f },
            { SoundId.PhaseEnemy, 0.5f }, { SoundId.Victory, 0.55f }, { SoundId.Defeat, 0.55f },
            { SoundId.AmbHolding, 0.35f }, { SoundId.AmbMaintenance, 0.55f }, { SoundId.AmbSecurity, 0.45f }, { SoundId.CombatPulse, 0.4f },
            { SoundId.MusicExplore, 0.5f },
        };

        private static readonly Dictionary<string, SoundId> RoomBeds = new Dictionary<string, SoundId>
        {
            { "Holding", SoundId.AmbHolding },
            { "Maintenance", SoundId.AmbMaintenance },
            { "Security", SoundId.AmbSecurity },
            { "Records", SoundId.AmbHolding },
            { "Server Hall", SoundId.AmbMaintenance },
            { "Lift Lobby", SoundId.AmbHolding },
            { "Labs", SoundId.AmbHolding },
            { "Specimen Hall", SoundId.AmbSecurity },
        };

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<SoundId, int> _counts = new Dictionary<SoundId, int>();
        private AudioSource _ambA, _ambB, _combat, _music;
        private float _ambBlend = 1f; // 1 = A fully audible
        private SoundId? _ambCurrent;
        private bool _currentOnA = true;
        private bool _combatOn;
        private float _combatLevel;
        private Transform _player;
        private int _next;

        public string CurrentAmbience => _ambCurrent?.ToString() ?? "";
        public bool CombatMusicOn => _combatOn;
        public bool MusicPlaying => _music != null && _music.isPlaying;
        public float MusicVolume => _music != null ? _music.volume : 0f;
        public int PlayCount(SoundId id) => _counts.TryGetValue(id, out int n) ? n : 0;
        public static float EffectsGain => GameSettings.EffectsVolume;

        public void Configure(string roomWhenNoPlayer) => defaultRoom = roomWhenNoPlayer;
        public static float AmbienceGain => GameSettings.AmbienceVolume;
        public static float MusicGain => GameSettings.MusicVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureLibrary();
            for (int i = 0; i < voices; i++) _pool.Add(NewSource($"Voice{i}", false));
            _ambA = NewSource("AmbienceA", true);
            _ambB = NewSource("AmbienceB", true);
            _combat = NewSource("CombatLoop", true);
            _combat.clip = Clip(SoundId.CombatPulse);
            _music = NewSource("Music", true);
            _music.clip = Clip(SoundId.MusicExplore);
            _music.ignoreListenerPause = true; // keeps playing, quietly, in the pause menu
            GameSettings.Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
            if (_player == null && !string.IsNullOrEmpty(defaultRoom) && RoomBeds.TryGetValue(defaultRoom, out SoundId bed)) SetAmbience(bed);
        }

        public static void EnsureLibrary()
        {
            if (Library.Count > 0 && Library[SoundId.Footstep][0] != null) return;
            Library.Clear();
            ClipGain.Clear();
            var gains = new Dictionary<string, float>();
            TextAsset table = Resources.Load<TextAsset>("Sounds/gains");
            if (table != null)
            {
                GainTable t = JsonUtility.FromJson<GainTable>(table.text);
                if (t?.entries != null) foreach (GainEntry e in t.entries) gains[e.key] = e.gain;
            }
            foreach (SoundId id in System.Enum.GetValues(typeof(SoundId)))
            {
                int n = Synth.Variants(id);
                var clips = new AudioClip[n];
                for (int v = 0; v < n; v++)
                {
                    // Recorded clip if one is mapped (Resources/Sounds/<SoundId>_<variant>), else the synthesised placeholder.
                    AudioClip rec = Recorded(id, v);
                    if (rec != null && gains.TryGetValue($"{id}_{v}", out float g)) ClipGain[rec] = g;
                    clips[v] = rec ?? Synth.CreateClip(id, v);
                    clips[v].hideFlags = HideFlags.DontUnloadUnusedAsset; // survive scene changes
                }
                Library[id] = clips;
            }
        }

        /// <summary>The recorded clip mapped to this sound, or null. Recorded sounds are Kenney CC0 packs (see ThirdParty/Kenney/Sounds).</summary>
        public static AudioClip Recorded(SoundId id, int variant) => Resources.Load<AudioClip>($"Sounds/{id}_{variant}");

        /// <summary>True when this sound plays a recorded file rather than the synth.</summary>
        public static bool IsRecorded(SoundId id, int variant = 0) => Recorded(id, variant) != null;

        public static AudioClip Clip(SoundId id, int variant = 0)
        {
            EnsureLibrary();
            AudioClip[] c = Library[id];
            return c[Mathf.Clamp(variant, 0, c.Length - 1)];
        }

        /// <summary>Extra gain for a recorded clip so it sits at the synth's loudness (1 for synth clips).</summary>
        public static float GainOf(AudioClip clip) => clip != null && ClipGain.TryGetValue(clip, out float g) ? g : 1f;

        public static float MixLevel(SoundId id) => Mix.TryGetValue(id, out float m) ? m : 0.5f;

        // ---------------------------------------------------------------- playback

        /// <summary>Plays a one-shot. With a position it's placed in the world (panned and attenuated); without, it's flat (UI).</summary>
        public void Play(SoundId id, Vector3? at = null, float volume = 1f, float pitchJitter = 0.06f)
        {
            _counts[id] = PlayCount(id) + 1;
            AudioClip[] clips = Library[id];
            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Count;

            bool ui = id == SoundId.UiMove || id == SoundId.UiConfirm;
            src.ignoreListenerPause = ui; // menu clicks still play while the game is paused
            src.clip = clips[Random.Range(0, clips.Length)];
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = Mathf.Clamp01(MixLevel(id) * volume * EffectsGain * GainOf(src.clip));
            if (at.HasValue)
            {
                src.transform.position = at.Value;
                src.spatialBlend = 0.7f;
            }
            else
            {
                src.transform.localPosition = Vector3.zero;
                src.spatialBlend = 0f;
            }
            src.Play();
        }

        public void SetCombat(bool on) => _combatOn = on;

        private void SetAmbience(SoundId bed)
        {
            if (_ambCurrent == bed) return;
            _ambCurrent = bed;
            // Start the new bed on whichever source is quieter and fade across.
            AudioSource target = _ambBlend >= 0.5f ? _ambB : _ambA;
            _currentOnA = target == _ambA;
            target.clip = Clip(bed);
            target.time = 0f;
            target.Play();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_player != null)
            {
                CameraZone zone = CameraZone.Find(_player.position);
                if (zone != null && RoomBeds.TryGetValue(zone.DisplayName, out SoundId bed)) SetAmbience(bed);
            }

            // Crossfade toward whichever source holds the current bed.
            float goal = _ambCurrent == null ? _ambBlend : _currentOnA ? 1f : 0f;
            _ambBlend = Mathf.MoveTowards(_ambBlend, goal, dt / Mathf.Max(0.01f, ambienceFade));

            float combatGoal = _combatOn ? 1f : 0f;
            _combatLevel = Mathf.MoveTowards(_combatLevel, combatGoal, dt / 0.8f);
            if (_combatLevel > 0f && !_combat.isPlaying) _combat.Play();
            if (_combatLevel <= 0f && _combat.isPlaying) _combat.Stop();

            float amb = AmbienceGain * (1f - 0.5f * _combatLevel); // duck the room under combat
            _ambA.volume = (_ambA.clip != null ? MixLevel(BedOf(_ambA.clip)) : 0f) * amb * _ambBlend;
            _ambB.volume = (_ambB.clip != null ? MixLevel(BedOf(_ambB.clip)) : 0f) * amb * (1f - _ambBlend);
            _combat.volume = MixLevel(SoundId.CombatPulse) * AmbienceGain * _combatLevel;
            // The exploration bed steps back under the combat loop and while paused.
            float musicDuck = (1f - 0.75f * _combatLevel) * (AudioListener.pause ? 0.4f : 1f);
            _music.volume = MixLevel(SoundId.MusicExplore) * MusicGain * musicDuck;
            if (!_music.isPlaying && _music.volume > 0f) _music.Play();
            if (_ambBlend >= 1f && _ambB.isPlaying) _ambB.Stop();
            if (_ambBlend <= 0f && _ambA.isPlaying) _ambA.Stop();
        }

        private static SoundId BedOf(AudioClip clip)
        {
            foreach (KeyValuePair<SoundId, AudioClip[]> kv in Library)
            {
                if (kv.Value[0] == clip) return kv.Key;
            }
            return SoundId.AmbHolding;
        }

        private AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = 26f;
            s.dopplerLevel = 0f;
            return s;
        }
    }

    /// <summary>Fire-and-forget access for gameplay code; does nothing when there's no AudioManager.</summary>
    public static class GameAudio
    {
        public static void Play(SoundId id, Vector3? at = null, float volume = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, at, volume);
        }

        public static void SetCombat(bool on)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetCombat(on);
        }
    }
}
