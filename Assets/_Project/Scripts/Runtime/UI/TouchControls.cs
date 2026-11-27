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
    /// Labels follow the context: exploring or in a conversation.
    /// Shown automatically on touch devices; <see cref="ForceVisible"/> shows them anywhere (tests, testing on desktop).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public static bool ForceVisible { get; set; }

        private GameObject _root;
        private Text _aLabel, _bLabel, _yLabel, _xLabel;
        private static readonly Color YIdle = new Color(1f, 1f, 1f, 0.18f);

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

            bool inDialogue = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen;
            _aLabel.text = inDialogue ? "NEXT" : "JUMP";
            _bLabel.text = "CROUCH";
            _yLabel.text = "USE";
            _xLabel.text = "ATTACK";
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

            // Face buttons, bottom-right: ATTACK and JUMP side by side under the thumb, CROUCH and USE above.
            _xLabel = Button(r, "X", "<Gamepad>/buttonWest", new Vector2(-150f, 40f), 118f, new Color(1f, 0.42f, 0.35f, 0.32f));
            _aLabel = Button(r, "A", "<Gamepad>/buttonSouth", new Vector2(-20f, 40f), 118f, new Color(1f, 0.74f, 0.45f, 0.3f));
            _bLabel = Button(r, "B", "<Gamepad>/buttonEast", new Vector2(-24f, 178f), 92f, new Color(1f, 1f, 1f, 0.18f));
            _yLabel = Button(r, "Y", "<Gamepad>/buttonNorth", new Vector2(-158f, 182f), 88f, YIdle);

            // Pause, top-right.
            Text pause = Button(r, "Pause", "<Gamepad>/start", new Vector2(-30f, 0f), 90f, new Color(1f, 1f, 1f, 0.2f), top: true);
            pause.text = "II";
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
