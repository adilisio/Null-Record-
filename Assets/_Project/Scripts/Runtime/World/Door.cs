using System.Collections;
using Game.Core;
using Game.Player;
using Game.Audio;
using UnityEngine;

namespace Game.World
{
    /// <summary>A sliding security door. Opens when the player's keycard level is high enough.</summary>
    public class Door : Interactable
    {
        [SerializeField, Min(0)] private int requiredKeycardLevel = 1;
        [Tooltip("The moving part (with its collider). Slides up when opened.")]
        [SerializeField] private Transform panel;
        [SerializeField] private float openDistance = 3f;
        [SerializeField] private float openDuration = 0.6f;
        [Tooltip("Optional emissive object tinted to show the lock state.")]
        [SerializeField] private Renderer statusLight;
        [SerializeField] private Color lockedColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color unlockedColor = new Color(0.2f, 1f, 0.4f);
        [Tooltip("Unique per scene. Opening sets the flag opened_<id>, so the door stays open after loading.")]
        [SerializeField] private string saveId = "";

        private bool _open;
        private MaterialPropertyBlock _block;

        public bool IsOpen => _open;
        public int RequiredKeycardLevel => requiredKeycardLevel;

        public void Configure(int level, Transform doorPanel, Renderer light, float slideDistance)
        {
            requiredKeycardLevel = level;
            panel = doorPanel;
            statusLight = light;
            openDistance = slideDistance;
        }

        private string OpenedFlag => $"opened_{saveId}";

        public void SetSaveId(string id) => saveId = id;

        private void Start()
        {
            PlayerInventory inv = GameObject.FindWithTag("Player")?.GetComponent<PlayerInventory>();
            if (!string.IsNullOrEmpty(saveId) && inv != null && inv.HasFlag(OpenedFlag))
            {
                _open = true;
                if (panel != null) panel.localPosition += Vector3.up * openDistance;
            }
            RefreshLight();
        }

        public override bool IsAvailable(PlayerInteractor player) => !_open && base.IsAvailable(player);

        public override string GetPrompt(PlayerInteractor player)
        {
            return CanOpen(player) ? "Open door" : $"Locked: Level {requiredKeycardLevel} access";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (_open) return;
            if (!CanOpen(player))
            {
                GameMessages.Show($"Access denied. Level {requiredKeycardLevel} keycard required.");
                GameAudio.Play(SoundId.DoorDenied, transform.position + Vector3.up * 1.5f);
                return;
            }
            _open = true;
            if (!string.IsNullOrEmpty(saveId)) player.Inventory.SetFlag(OpenedFlag);
            RefreshLight();
            GameAudio.Play(SoundId.DoorOpen, transform.position + Vector3.up * 1.5f);
            StartCoroutine(Slide());
        }

        private bool CanOpen(PlayerInteractor player)
        {
            return player.Inventory != null && player.Inventory.KeycardLevel >= requiredKeycardLevel;
        }

        private IEnumerator Slide()
        {
            if (panel == null) yield break;
            Vector3 from = panel.localPosition;
            Vector3 to = from + Vector3.up * openDistance;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, openDuration))
            {
                panel.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            panel.localPosition = to;
        }

        private void RefreshLight()
        {
            if (statusLight == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            Color c = _open ? unlockedColor : lockedColor;
            statusLight.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_EmissionColor", c * 3f);
            statusLight.SetPropertyBlock(_block);
        }
    }
}
