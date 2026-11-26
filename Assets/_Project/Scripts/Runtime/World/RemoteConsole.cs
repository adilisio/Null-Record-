using System.Collections;
using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Controls for something big (a bridge, a freight shutter) that the remote override operates from a distance.
    /// Without the tool it can only be reached by standing at it, and it's locked out. With the tool it can be
    /// used from up to <see cref="Interactable.Range"/> away. Opening slides <c>moving</c> by <c>openOffset</c>
    /// and is remembered via opened_&lt;id&gt;.
    /// </summary>
    public class RemoteConsole : Interactable
    {
        public const string OverrideTool = "override";

        [SerializeField] private string displayName = "bridge controls";
        [SerializeField] private string requiredTool = OverrideTool;
        [Tooltip("Reach without the tool (standing at the console).")]
        [SerializeField] private float nearRange = 1.5f;
        [SerializeField] private Transform moving;
        [SerializeField] private Vector3 openOffset = Vector3.left * 7f;
        [SerializeField] private float openDuration = 1.2f;
        [SerializeField] private Renderer statusLight;
        [SerializeField] private string saveId = "";

        private bool _open;
        private MaterialPropertyBlock _block;

        public bool IsOpen => _open;
        private string OpenedFlag => $"opened_{saveId}";

        public void Configure(string name, Transform target, Vector3 offset, Renderer light, string id)
        {
            displayName = name;
            moving = target;
            openOffset = offset;
            statusLight = light;
            saveId = id;
        }

        private void Start()
        {
            PlayerInventory inv = GameObject.FindWithTag("Player")?.GetComponent<PlayerInventory>();
            if (!string.IsNullOrEmpty(saveId) && inv != null && inv.HasFlag(OpenedFlag))
            {
                _open = true;
                if (moving != null) moving.position += openOffset;
            }
            Refresh();
        }

        private bool HasTool(PlayerInteractor player) => player.Inventory != null && player.Inventory.HasTool(requiredTool);

        public override bool IsAvailable(PlayerInteractor player)
        {
            if (_open || !base.IsAvailable(player)) return false;
            if (HasTool(player)) return true;
            return Vector3.Distance(player.transform.position + Vector3.up, Point) <= nearRange;
        }

        public override string GetPrompt(PlayerInteractor player) =>
            HasTool(player) ? $"Remote override: {displayName}" : $"{char.ToUpper(displayName[0])}{displayName.Substring(1)}: locked out";

        public override void Interact(PlayerInteractor player)
        {
            if (_open) return;
            if (!HasTool(player))
            {
                GameMessages.Show($"The {displayName} won't answer. A remote override could take control.");
                GameAudio.Play(SoundId.DoorDenied, Point);
                return;
            }
            _open = true;
            if (!string.IsNullOrEmpty(saveId)) player.Inventory.SetFlag(OpenedFlag);
            GameMessages.Show($"Override accepted: {displayName}.");
            GameAudio.Play(SoundId.DoorOpen, Point);
            Refresh();
            StartCoroutine(Slide());
        }

        private IEnumerator Slide()
        {
            if (moving == null) yield break;
            Vector3 from = moving.position;
            Vector3 to = from + openOffset;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, openDuration))
            {
                moving.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            moving.position = to;
        }

        private void Refresh()
        {
            if (statusLight == null) return;
            _block ??= new MaterialPropertyBlock();
            Color c = _open ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.6f, 0.1f);
            statusLight.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_EmissionColor", c * 3f);
            statusLight.SetPropertyBlock(_block);
        }
    }
}
