using System;
using System.Collections.Generic;
using Game.CameraSystem;
using Game.Companions;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Save
{
    /// <summary>
    /// Frequent autosave. Loads the save (if any) when the scene starts, and saves:
    /// on entering a room, after picking things up or changing story flags, after recruiting,
    /// and before and after encounters. Saves wait until the player is standing still in normal
    /// control (not mid-air, in dialogue, in combat or respawning), except explicit <see cref="SaveNow"/>.
    ///
    /// Entering a room also moves the respawn point to where the player first stood in it
    /// (unless a Checkpoint inside that room already set one).
    /// </summary>
    [DefaultExecutionOrder(-200)] // restore flags before world objects read them in Start
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [SerializeField] private bool loadOnStart = true;
        [Tooltip("Minimum seconds between autosaves; requests in between are merged.")]
        [SerializeField] private float minInterval = 1f;
        [SerializeField] private float indicatorSeconds = 1.2f;

        private PlayerMotor _motor;
        private PlayerInventory _inventory;
        private PlayerRespawn _respawn;
        private PlayerInteractor _interactor;
        private SaveData _loaded;

        private string _pendingReason;
        private float _lastSaveTime = -999f;
        private CameraZone _room;
        private bool _roomEntryPending;
        private Text _indicator;
        private float _indicatorUntil;

        public string LastSaveReason { get; private set; }
        public int SaveCount { get; private set; }
        public bool LoadedFromSave { get; private set; }
        public string CurrentRoom => _room != null ? _room.name : "";
        public string CurrentRoomName => _room != null ? _room.DisplayName : "";

        public event Action<string> Saved;

        /// <summary>Ask for an autosave when it's safe. Safe to call when there's no SaveManager.</summary>
        public static void Request(string reason)
        {
            if (Instance != null) Instance.RequestSave(reason);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            GameObject p = GameObject.FindWithTag("Player");
            if (p == null)
            {
                Debug.LogError("[Save] No Player in the scene.", this);
                enabled = false;
                return;
            }
            _motor = p.GetComponent<PlayerMotor>();
            _inventory = p.GetComponent<PlayerInventory>();
            _respawn = p.GetComponent<PlayerRespawn>();
            _interactor = p.GetComponent<PlayerInteractor>();

            if (loadOnStart) _loaded = SaveSystem.Read();
            if (_loaded != null && _loaded.scene == SceneManager.GetActiveScene().path)
            {
                // Flags first, so pickups/doors/encounters can check them in their Start.
                _inventory.Restore(_loaded.keycardLevel, (HazardType)_loaded.protections, _loaded.scrap, _loaded.tools, _loaded.flags, _loaded.xp);
                LoadedFromSave = true;
            }
            else
            {
                _loaded = null;
            }
            BuildIndicator();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
        }

        private void Start()
        {
            if (_loaded != null)
            {
                _motor.Teleport(_loaded.position);
                _motor.Face(_loaded.facing);
                _respawn.SetCheckpoint(_loaded.checkpoint);
                // Every recruited companion comes back: the saved one follows, the others wait at the terminal.
                foreach (CompanionNPC npc in FindObjectsByType<CompanionNPC>(FindObjectsSortMode.None))
                {
                    if (!_inventory.HasFlag(npc.RecruitFlag)) continue;
                    bool active = npc.GetComponent<CompanionFollower>().CombatUnit == _loaded.companion;
                    npc.Recruit(_interactor, announce: false, makeActive: active);
                }
                CompanionFollower.Active?.WarpBehindPlayer();
                GameMessages.Show("Save loaded.");
            }
            _room = CameraZone.Find(_motor.transform.position);
            _inventory.Changed += OnInventoryChanged;
        }

        private void OnInventoryChanged() => RequestSave("progress");

        public void RequestSave(string reason)
        {
            _pendingReason = _pendingReason == null ? reason : _pendingReason;
        }

        /// <summary>Saves immediately (e.g. right before an encounter starts).</summary>
        public void SaveNow(string reason)
        {
            SaveData d = Capture(reason);
            SaveSystem.Write(d);
            _lastSaveTime = Time.unscaledTime;
            _pendingReason = null;
            LastSaveReason = reason;
            SaveCount++;
            _indicatorUntil = Time.unscaledTime + indicatorSeconds;
            if (_indicator != null) _indicator.gameObject.SetActive(true);
            Saved?.Invoke(reason);
        }

        private void Update()
        {
            if (_motor == null) return;
            bool settled = _motor.ControlEnabled && !_motor.ExternallyControlled && _motor.State == MotorState.Grounded;

            // Room tracking: entering a new room moves the respawn point and autosaves.
            CameraZone zone = CameraZone.Find(_motor.transform.position);
            if (zone != null && zone != _room)
            {
                _room = zone;
                _roomEntryPending = true;
            }
            if (_roomEntryPending && settled)
            {
                _roomEntryPending = false;
                // Respawn at the room entry, unless a checkpoint in this room already set one.
                if (!_room.Rect.Contains(_respawn.Checkpoint)) _respawn.SetCheckpoint(_motor.transform.position);
                RequestSave("room");
            }

            if (_pendingReason != null && settled && Time.unscaledTime - _lastSaveTime >= minInterval) SaveNow(_pendingReason);

            if (_indicator != null && _indicator.gameObject.activeSelf && Time.unscaledTime > _indicatorUntil) _indicator.gameObject.SetActive(false);
        }

        private SaveData Capture(string reason)
        {
            bool grounded = _motor.State == MotorState.Grounded || _motor.ExternallyControlled;
            var tools = new List<string>(_inventory.Tools);
            var flags = new List<string>(_inventory.Flags);
            CompanionFollower c = CompanionFollower.Active;
            return new SaveData
            {
                scene = SceneManager.GetActiveScene().path,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                reason = reason,
                room = CurrentRoom,
                roomName = CurrentRoomName,
                position = grounded ? _motor.transform.position : _respawn.Checkpoint,
                facing = _motor.Facing,
                checkpoint = _respawn.Checkpoint,
                keycardLevel = _inventory.KeycardLevel,
                protections = (int)_inventory.Protections,
                scrap = _inventory.Scrap,
                xp = _inventory.Xp,
                tools = tools.ToArray(),
                flags = flags.ToArray(),
                companion = c != null ? c.CombatUnit : "",
            };
        }

        private void BuildIndicator()
        {
            var canvasGo = new GameObject("SaveIndicator");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvasGo.transform, false);
            _indicator = go.GetComponent<Text>();
            _indicator.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _indicator.fontSize = 20;
            _indicator.alignment = TextAnchor.UpperRight;
            _indicator.color = new Color(0.75f, 0.85f, 0.9f, 0.9f);
            _indicator.text = "SAVING...";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            // Left of the touch pause button (top-right corner).
            rt.offsetMin = new Vector2(-380f, -56f);
            rt.offsetMax = new Vector2(-120f, -20f);
            go.SetActive(false);
        }
    }
}
