using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Exploration HUD on a screen canvas (so it scales on phones and shows in test captures):
    /// the interaction prompt as a pill at the bottom, a short message stack at the top, and an optional debug line.
    /// </summary>
    public class GreyboxHUD : MonoBehaviour
    {
        private struct Message
        {
            public string Text;
            public float Until;
        }

        [SerializeField] private PlayerInteractor player;
        [SerializeField] private float messageDuration = 3f;
        [SerializeField] private bool showDebug = true;

        private readonly List<Message> _messages = new List<Message>();
        private Image _pill;
        private Text _prompt;
        private Text _feed;
        private Text _debug;
        private Text _hp;
        private Image[] _pips = new Image[0];
        private Game.Combat.PlayerCombat _combat;

        /// <summary>What the prompt pill says right now ("" when hidden).</summary>
        public string PromptText => _pill != null && _pill.gameObject.activeSelf ? _prompt.text : "";
        public string FeedText => _feed != null ? _feed.text : "";
        /// <summary>The HP label next to the pips, e.g. "HP 3/5".</summary>
        public string HpText => _hp != null ? _hp.text : "";

        public void SetPlayer(PlayerInteractor p) => player = p;

        private void OnEnable() => GameMessages.Posted += OnMessage;
        private void OnDisable() => GameMessages.Posted -= OnMessage;

        private void OnMessage(string text)
        {
            _messages.Add(new Message { Text = text, Until = Time.time + messageDuration });
            if (_messages.Count > 4) _messages.RemoveAt(0);
        }

        private void Start() => Build();

        private void Build()
        {
            Canvas canvas = MenuWidgets.Canvas(transform, "HUDCanvas", 50, Camera.main);
            Transform r = canvas.transform;

            _feed = MenuWidgets.Label(r, "Messages", "", 24, MenuWidgets.TextColor, TextAnchor.UpperCenter);
            // Upper middle, clear of the objective and HP (left) and the pause button (right).
            MenuWidgets.Anchor(_feed.rectTransform, new Vector2(0.24f, 1f), new Vector2(0.76f, 1f), new Vector2(0f, -260f), new Vector2(0f, -145f));
            var shadow = _feed.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            _pill = MenuWidgets.Panel(r, "PromptPill", new Color(0.03f, 0.04f, 0.05f, 0.78f));
            MenuWidgets.Anchor(_pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-330f, 96f), new Vector2(330f, 146f));
            _prompt = MenuWidgets.Label(_pill.transform, "Prompt", "", 24, MenuWidgets.Accent, TextAnchor.MiddleCenter);
            MenuWidgets.Stretch(_prompt.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            _pill.gameObject.SetActive(false);

            // HP pips, top-left under the objective line.
            _hp = MenuWidgets.Label(r, "HP", "", 26, new Color(1f, 0.45f, 0.4f), TextAnchor.UpperLeft);
            MenuWidgets.Anchor(_hp.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -112f), new Vector2(0f, -78f));
            _hp.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            _hp.fontSize = 16;
            _pips = new Image[12];
            for (int i = 0; i < _pips.Length; i++)
            {
                _pips[i] = MenuWidgets.Panel(r, "Pip" + i, Color.white);
                MenuWidgets.Anchor(_pips[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f + i * 26f, -104f), new Vector2(116f + i * 26f, -84f));
                _pips[i].raycastTarget = false;
                _pips[i].gameObject.SetActive(false);
            }

            _debug = MenuWidgets.Label(r, "Debug", "", 14, MenuWidgets.Muted, TextAnchor.UpperLeft);
            MenuWidgets.Anchor(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -40f), new Vector2(-12f, -8f));
        }

        private void Update()
        {
            if (_feed == null) return;
            _messages.RemoveAll(m => Time.time > m.Until);
            var lines = new List<string>(_messages.Count);
            foreach (Message m in _messages) lines.Add(m.Text);
            _feed.text = string.Join("\n", lines);

            if (_combat == null && player != null) _combat = player.GetComponent<Game.Combat.PlayerCombat>();
            // Hidden while a bark (intercom, banter) sits over the same corner.
            bool showHp = _combat != null && Time.timeScale > 0f && (Game.Dialogue.DialogueUI.Instance == null || !Game.Dialogue.DialogueUI.Instance.BarkVisible);
            _hp.text = showHp ? $"HP {_combat.Hp}/{_combat.MaxHp}" : "";
            for (int i = 0; i < _pips.Length; i++)
            {
                bool on = showHp && i < _combat.MaxHp;
                if (_pips[i].gameObject.activeSelf != on) _pips[i].gameObject.SetActive(on);
                if (on) _pips[i].color = i < _combat.Hp ? new Color(1f, 0.35f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.15f);
            }

            bool prompt = player != null && player.Current != null && player.isActiveAndEnabled && Time.timeScale > 0f;
            if (_pill.gameObject.activeSelf != prompt) _pill.gameObject.SetActive(prompt);
            if (prompt) _prompt.text = $"{InputHints.Interact}:  {player.Current.GetPrompt(player)}";

            if (!showDebug || !GameSettings.DebugHud || player == null)
            {
                _debug.text = "";
                return;
            }
            PlayerInventory inv = player.Inventory;
            PlayerMotor motor = player.Motor;
            string line = inv != null ? $"Keycard L{inv.KeycardLevel}   Protection: {inv.Protections}   Scrap: {inv.Scrap}   LV {Game.Combat.Progression.Level(inv.Xp)} ({inv.Xp} XP)" : "";
            if (motor != null)
            {
                line += $"   |   {motor.State}{(motor.IsCrouching ? " (crouch)" : "")}   v=({motor.Velocity.x:0.0}, {motor.Velocity.y:0.0})";
            }
            _debug.text = line;
        }
    }
}
