using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Temporary IMGUI HUD for greybox testing: interaction prompt, inventory line, message feed.
    /// To be replaced by the real UI.
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
        private GUIStyle _prompt;
        private GUIStyle _small;

        public void SetPlayer(PlayerInteractor p) => player = p;

        private void OnEnable() => GameMessages.Posted += OnMessage;
        private void OnDisable() => GameMessages.Posted -= OnMessage;

        private void OnMessage(string text) => _messages.Add(new Message { Text = text, Until = Time.time + messageDuration });

        private void OnGUI()
        {
            if (_prompt == null)
            {
                _prompt = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                _small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            }

            _messages.RemoveAll(m => Time.time > m.Until);
            for (int i = 0; i < _messages.Count; i++)
            {
                GUI.Label(new Rect(0, 40 + i * 30, Screen.width, 30), _messages[i].Text, _prompt);
            }

            if (player == null) return;

            if (player.Current != null)
            {
                GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 40), "[E]  " + player.Current.GetPrompt(player), _prompt);
            }

            if (!showDebug || !Core.GameSettings.DebugHud) return;
            PlayerInventory inv = player.Inventory;
            PlayerMotor motor = player.Motor;
            string line = inv != null ? $"Keycard L{inv.KeycardLevel}   Protection: {inv.Protections}   Scrap: {inv.Scrap}   LV {Game.Combat.Progression.Level(inv.Xp)} ({inv.Xp} XP)" : "";
            if (motor != null)
            {
                line += $"   |   {motor.State}{(motor.IsCrouching ? " (crouch)" : "")}   v=({motor.Velocity.x:0.0}, {motor.Velocity.y:0.0})";
            }
            GUI.Label(new Rect(10, 10, Screen.width - 20, 24), line, _small);
        }
    }
}
