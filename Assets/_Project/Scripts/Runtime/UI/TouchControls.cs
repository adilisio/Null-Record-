using Game.Combat;
using Game.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// On-screen stick and buttons for phones and tablets. They drive a virtual gamepad through the
    /// Input System, so every existing gamepad binding (movement, combat, dialogue, menus) works unchanged.
    /// Labels follow the context: exploring, in combat, or in a conversation.
    /// Shown automatically on touch devices; <see cref="ForceVisible"/> shows them anywhere (tests, testing on desktop).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public static bool ForceVisible { get; set; }

        private GameObject _root;
        private Text _aLabel, _bLabel, _yLabel;
        private Image _yButton;
        private static readonly Color YIdle = new Color(1f, 1f, 1f, 0.18f);
        private GameObject _combatRow;

        public bool Visible => _root != null && _root.activeSelf;

        private static bool _touchSeen;

        /// <summary>Forget that the screen was touched (tests that simulate a touch call this afterwards).</summary>
        public static void ResetTouchSeen() => _touchSeen = false;

        /// <summary>On phones/tablets, or once the screen has actually been touched (desktop browsers often report a touchscreen).</summary>
        public static bool ShouldShow => ForceVisible || Application.isMobilePlatform || _touchSeen;

        private void Start()
        {
            Build();
        }

        private void Update()
        {
            if (!_touchSeen && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) _touchSeen = true;
            bool show = ShouldShow && Time.timeScale > 0f;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            bool inCombat = EncounterController.Active != null;
            bool inDialogue = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen;
            _combatRow.SetActive(inCombat);
            _aLabel.text = inCombat ? "OK" : inDialogue ? "NEXT" : "JUMP";
            _bLabel.text = inCombat ? "BACK" : "CROUCH";
            _yLabel.text = inCombat ? "END TURN" : "USE";
            // Pulse END TURN once the whole party has acted.
            bool pulse = inCombat && EncounterController.Active.AllActed;
            _yButton.color = pulse ? Color.Lerp(YIdle, new Color(1f, 0.74f, 0.45f, 0.6f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : YIdle;
        }

        private void Build()
        {
            Canvas canvas = MenuWidgets.Canvas(transform, "TouchCanvas", 70, Camera.main);
            _root = canvas.gameObject;
            Transform r = canvas.transform;

            // Stick, bottom-left.
            Image ring = MenuWidgets.Panel(r, "StickRing", new Color(1f, 1f, 1f, 0.12f));
            ring.sprite = MenuWidgets.Circle;
            MenuWidgets.Anchor(ring.rectTransform, Vector2.zero, Vector2.zero, new Vector2(60f, 50f), new Vector2(280f, 270f));
            Image knob = MenuWidgets.Panel(ring.transform, "Knob", new Color(1f, 1f, 1f, 0.32f));
            knob.sprite = MenuWidgets.Circle;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(96f, 96f);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 80f;

            // Face buttons, bottom-right (positions echo a gamepad's diamond).
            _aLabel = Button(r, "A", "<Gamepad>/buttonSouth", new Vector2(-140f, 50f), 112f, new Color(1f, 0.74f, 0.45f, 0.3f));
            _bLabel = Button(r, "B", "<Gamepad>/buttonEast", new Vector2(-24f, 150f), 92f, new Color(1f, 1f, 1f, 0.18f));
            _yLabel = Button(r, "Y", "<Gamepad>/buttonNorth", new Vector2(-256f, 150f), 88f, YIdle);
            _yButton = _yLabel.transform.parent.GetComponent<Image>();

            // Pause, top-right.
            Text pause = Button(r, "Pause", "<Gamepad>/start", new Vector2(-30f, 0f), 90f, new Color(1f, 1f, 1f, 0.2f), top: true);
            pause.text = "II";

            // Combat-only strip, bottom centre (clear of the grid's right-hand tiles): change action and switch unit.
            _combatRow = new GameObject("CombatRow", typeof(RectTransform));
            _combatRow.transform.SetParent(r, false);
            MenuWidgets.Stretch((RectTransform)_combatRow.transform, Vector2.zero, Vector2.zero);
            // The encounter's own action bar (MOVE / skills) sits just left of this.
            CentreButton(_combatRow.transform, "Unit", "<Gamepad>/leftStickPress", 250f, "UNIT");
            _combatRow.SetActive(false);
        }

        private static void CentreButton(Transform parent, string name, string controlPath, float x, string label)
        {
            Image img = MenuWidgets.Panel(parent, name, new Color(1f, 1f, 1f, 0.18f));
            img.sprite = MenuWidgets.Circle;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 24f);
            rt.sizeDelta = new Vector2(68f, 68f);
            img.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;
            Text t = MenuWidgets.Label(img.transform, "Label", label, 18, new Color(0.92f, 0.94f, 0.96f, 0.95f), TextAnchor.MiddleCenter);
            MenuWidgets.Stretch(t.rectTransform, Vector2.zero, Vector2.zero);
        }

        /// <summary>A round on-screen button anchored to the bottom-right (or top-right) corner.</summary>
        private static Text Button(Transform parent, string name, string controlPath, Vector2 offsetFromCorner, float size, Color color, bool top = false)
        {
            Image img = MenuWidgets.Panel(parent, name, color);
            img.sprite = MenuWidgets.Circle;
            RectTransform rt = img.rectTransform;
            Vector2 corner = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = new Vector2(1f, top ? 1f : 0f);
            rt.anchoredPosition = new Vector2(offsetFromCorner.x, top ? -20f - offsetFromCorner.y : offsetFromCorner.y);
            rt.sizeDelta = new Vector2(size, size);
            var b = img.gameObject.AddComponent<OnScreenButton>();
            b.controlPath = controlPath;
            Text t = MenuWidgets.Label(img.transform, "Label", name, size > 100f ? 22 : 17, new Color(0.92f, 0.94f, 0.96f, 0.95f), TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            MenuWidgets.Stretch(t.rectTransform, Vector2.zero, Vector2.zero);
            return t;
        }
    }
}
