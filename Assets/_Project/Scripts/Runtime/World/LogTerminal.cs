using Game.Dialogue;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A readable log: plays one entry of Dialogue/logs.json (start node = the log id) in the dialogue box and
    /// sets <c>log_&lt;id&gt;</c> the first time. Logs are written by staff ("log_staff") or earlier subjects ("log_subject").
    /// </summary>
    public class LogTerminal : Interactable
    {
        public const string FlagPrefix = "log_";
        /// <summary>Logs placed in the slice (end card count).</summary>
        public const int Total = 5;

        [SerializeField] private TextAsset logs;
        [SerializeField] private string logId;
        [SerializeField] private string title = "log";
        [SerializeField] private Renderer screen;

        private DialogueScript _script;
        private PlayerInteractor _player;

        public string LogId => logId;
        public string Flag => FlagPrefix + logId;

        public void Configure(TextAsset json, string id, string displayTitle, Renderer screenRenderer)
        {
            logs = json;
            logId = id;
            title = displayTitle;
            screen = screenRenderer;
        }

        public bool IsRead(PlayerInteractor player) => player != null && player.Inventory != null && player.Inventory.HasFlag(Flag);

        public override string GetPrompt(PlayerInteractor player) => IsRead(player) ? $"Read again: {title}" : $"Read: {title}";

        public override void Interact(PlayerInteractor player)
        {
            if (_script == null && logs != null) _script = DialogueScript.FromJson(logs.text);
            if (_script == null) return;
            DialogueUI.Ensure().Play(_script, logId, player, () => player.Inventory?.SetFlag(Flag));
        }

        private void Update()
        {
            // Unread screens pulse; read ones settle to a steady dim glow.
            if (screen == null) return;
            if (_player == null)
            {
                GameObject go = GameObject.FindWithTag("Player");
                if (go == null) return;
                _player = go.GetComponent<PlayerInteractor>();
            }
            float k = IsRead(_player) ? 0.6f : 1.2f + Mathf.Sin(Time.time * 3f) * 0.8f;
            screen.material.SetColor("_EmissionColor", new Color(0.35f, 0.75f, 1f) * k);
        }
    }
}
