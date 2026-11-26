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

        /// <summary>What the prompt pill says right now ("" when hidden).</summary>
        public string PromptText => _pill != null && _pill.gameObject.activeSelf ? _prompt.text : "";
        public string FeedText => _feed != null ? _feed.text : "";

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
            // Below the combat banner and help bar, between the combat log (right) and the screen's left edge.
            MenuWidgets.Anchor(_feed.rectTransform, new Vector2(0.24f, 1f), new Vector2(0.76f, 1f), new Vector2(0f, -260f), new Vector2(0f, -145f));
            var shadow = _feed.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            _pill = MenuWidgets.Panel(r, "PromptPill", new Color(0.03f, 0.04f, 0.05f, 0.78f));
            MenuWidgets.Anchor(_pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-330f, 96f), new Vector2(330f, 146f));
            _prompt = MenuWidgets.Label(_pill.transform, "Prompt", "", 24, MenuWidgets.Accent, TextAnchor.MiddleCenter);
            MenuWidgets.Stretch(_prompt.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            _pill.gameObject.SetActive(false);

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
