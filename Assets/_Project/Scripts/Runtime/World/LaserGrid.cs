using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A laser grid across a doorway: a solid barrier until the player fools its sensors with the right
    /// hacking tool (by default the signal spoofer). Like doors, it stays open after loading via opened_&lt;id&gt;.
    /// </summary>
    public class LaserGrid : Interactable
    {
        public const string SpooferTool = "spoofer";

        [SerializeField] private string requiredTool = SpooferTool;
        [SerializeField] private string toolName = "signal spoofer";
        [Tooltip("Blocks the player while the grid is live.")]
        [SerializeField] private Collider barrier;
        [Tooltip("Beam visuals, hidden once spoofed.")]
        [SerializeField] private GameObject beams;
        [SerializeField] private Renderer statusLight;
        [SerializeField] private string saveId = "";

        private bool _open;
        private MaterialPropertyBlock _block;

        public bool IsOpen => _open;
        public string RequiredTool => requiredTool;
        private string OpenedFlag => $"opened_{saveId}";

        public void Configure(string tool, string displayName, Collider wall, GameObject beamRoot, Renderer light, string id)
        {
            requiredTool = tool;
            toolName = displayName;
            barrier = wall;
            beams = beamRoot;
            statusLight = light;
            saveId = id;
        }

        private void Start()
        {
            PlayerInventory inv = GameObject.FindWithTag("Player")?.GetComponent<PlayerInventory>();
            if (!string.IsNullOrEmpty(saveId) && inv != null && inv.HasFlag(OpenedFlag)) SetOpen(true);
            else Refresh();
        }

        public override bool IsAvailable(PlayerInteractor player) => !_open && base.IsAvailable(player);

        public override string GetPrompt(PlayerInteractor player) =>
            CanOpen(player) ? $"Spoof the laser grid ({toolName})" : $"Laser grid: {toolName} needed";

        public override void Interact(PlayerInteractor player)
        {
            if (_open) return;
            if (!CanOpen(player))
            {
                GameMessages.Show($"The grid's sensors are live. A {toolName} could fool them.");
                GameAudio.Play(SoundId.DoorDenied, transform.position + Vector3.up * 1.5f);
                return;
            }
            if (!string.IsNullOrEmpty(saveId)) player.Inventory.SetFlag(OpenedFlag);
            GameMessages.Show("Grid spoofed. The beams drop.");
            GameAudio.Play(SoundId.Zap, transform.position + Vector3.up * 1.5f);
            SetOpen(true);
        }

        private bool CanOpen(PlayerInteractor player) => player.Inventory != null && player.Inventory.HasTool(requiredTool);

        private void SetOpen(bool open)
        {
            _open = open;
            if (barrier != null) barrier.enabled = !open;
            if (beams != null) beams.SetActive(!open);
            Refresh();
        }

        private void Refresh()
        {
            if (statusLight == null) return;
            _block ??= new MaterialPropertyBlock();
            Color c = _open ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.15f, 0.1f);
            statusLight.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_EmissionColor", c * 3f);
            statusLight.SetPropertyBlock(_block);
        }
    }
}
