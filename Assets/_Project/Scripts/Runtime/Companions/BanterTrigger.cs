using Game.Dialogue;
using Game.Player;
using UnityEngine;

namespace Game.Companions
{
    /// <summary>
    /// Plays a companion remark (a "bark") once, the first time the player enters an area with that
    /// companion in the party. Lines live in a dialogue JSON file; each trigger names one node.
    /// </summary>
    public class BanterTrigger : MonoBehaviour
    {
        [Tooltip("Side-on area (world X/Y).")]
        [SerializeField] private Rect area = new Rect(0f, 0f, 2f, 4f);
        [SerializeField] private TextAsset lines;
        [SerializeField] private string node;
        [Tooltip("Only plays while this flag is set (e.g. the companion is recruited).")]
        [SerializeField] private string requireFlag = "companion_guard";
        [Tooltip("Only plays while this companion (combat roster key) is the one following. Empty = any.")]
        [SerializeField] private string speakerUnit = "Guard";

        private DialogueScript _script;
        private PlayerInventory _player;

        private string PlayedFlag => $"banter_{node}";

        public void Configure(Rect newArea, TextAsset json, string nodeId, string require)
        {
            area = newArea;
            lines = json;
            node = nodeId;
            requireFlag = require;
        }

        private void Start()
        {
            if (lines != null) _script = DialogueScript.FromJson(lines.text);
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            if (_script == null || _player == null || _player.HasFlag(PlayedFlag)) return;
            if (!string.IsNullOrEmpty(requireFlag) && !_player.HasFlag(requireFlag)) return;
            if (CompanionFollower.Active == null || !CompanionFollower.Active.IsFollowing) return;
            if (!string.IsNullOrEmpty(speakerUnit) && CompanionFollower.Active.CombatUnit != speakerUnit) return;

            Vector3 pos = _player.transform.position;
            if (!area.Contains(new Vector2(pos.x, pos.y + 0.5f))) return;

            DialogueNode n = _script.Get(node);
            DialogueUI ui = DialogueUI.Ensure();
            if (n == null || ui.IsOpen) return;
            ui.Bark(n.speaker, n.text);
            _player.SetFlag(PlayedFlag);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(area.center.x, area.center.y, 0f), new Vector3(area.width, area.height, 0.1f));
        }
    }
}
