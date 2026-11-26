using Game.Core;
using Game.Player;
using Game.Audio;
using UnityEngine;

namespace Game.World
{
    public enum PickupKind
    {
        Keycard,
        Protection,
        Tool,
        Scrap,
    }

    /// <summary>Something the player picks up with Interact: keycards, hazard gear, tools, scrap.</summary>
    public class ItemPickup : Interactable
    {
        [SerializeField] private PickupKind kind = PickupKind.Keycard;
        [SerializeField] private string displayName = "Level 1 Keycard";
        [Tooltip("Keycard level, or scrap amount.")]
        [SerializeField] private int amount = 1;
        [SerializeField] private HazardType protection = HazardType.Heat;
        [SerializeField] private string toolId = "";
        [SerializeField] private float bobHeight = 0.1f;
        [SerializeField] private float spinSpeed = 60f;
        [Tooltip("Unique per scene. Taking the pickup sets the flag taken_<id>, which keeps it gone after loading.")]
        [SerializeField] private string saveId = "";

        private Vector3 _origin;

        public void ConfigureKeycard(int level)
        {
            kind = PickupKind.Keycard;
            amount = level;
            displayName = $"Level {level} Keycard";
        }

        public void ConfigureProtection(HazardType hazard)
        {
            kind = PickupKind.Protection;
            protection = hazard;
            displayName = hazard.ProtectionName();
        }

        public string SaveId => saveId;
        private string TakenFlag => $"taken_{saveId}";

        public void SetSaveId(string id) => saveId = id;

        private void Start()
        {
            _origin = transform.position;
            PlayerInventory inv = GameObject.FindWithTag("Player")?.GetComponent<PlayerInventory>();
            if (!string.IsNullOrEmpty(saveId) && inv != null && inv.HasFlag(TakenFlag)) Destroy(gameObject);
        }

        private void Update()
        {
            transform.position = _origin + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        public override string GetPrompt(PlayerInteractor player) => $"Take {displayName}";

        public override void Interact(PlayerInteractor player)
        {
            PlayerInventory inv = player.Inventory;
            if (inv == null) return;

            switch (kind)
            {
                case PickupKind.Keycard: inv.GrantKeycard(amount); break;
                case PickupKind.Protection: inv.GrantProtection(protection); break;
                case PickupKind.Tool: inv.GrantTool(toolId); break;
                case PickupKind.Scrap: inv.AddScrap(amount); break;
            }

            if (!string.IsNullOrEmpty(saveId)) inv.SetFlag(TakenFlag);
            GameAudio.Play(SoundId.Pickup, transform.position);
            GameMessages.Show(kind == PickupKind.Scrap ? $"+{amount} scrap" : $"Acquired: {displayName}");
            Destroy(gameObject);
        }
    }
}
