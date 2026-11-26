using System;
using System.Globalization;
using Game.Core;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Title screen: Continue (when a save exists), New Game (asks before erasing a save), Settings, Quit.
    /// Mouse, keyboard (arrows/WASD + Enter/Space, Esc to go back) and gamepad all work.
    /// </summary>
    public class TitleMenu : MonoBehaviour
    {
        private RectTransform _main;
        private RectTransform _confirm;
        private SettingsPanel _settings;
        private Text _saveInfo;
        private InputAction _back;
        private SaveData _save;

        public Button ContinueButton { get; private set; }
        public Button NewGameButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Button ConfirmNewGameButton { get; private set; }
        public Button CancelNewGameButton { get; private set; }
        public SettingsPanel Settings => _settings;
        public bool ConfirmVisible => _confirm != null && _confirm.gameObject.activeSelf;
        public bool SettingsVisible => _settings != null && _settings.Root.gameObject.activeSelf;
        public string SaveInfo => _saveInfo != null ? _saveInfo.text : "";

        private void Awake()
        {
            Time.timeScale = 1f;
            GameSettings.Apply();
            MenuWidgets.EnsureEventSystem();
            Build();
            _back = new InputAction("Back", InputActionType.Button);
            _back.AddBinding("<Keyboard>/escape");
            _back.AddBinding("<Gamepad>/buttonEast");
        }

        private void OnEnable() => _back?.Enable();
        private void OnDisable() => _back?.Disable();
        private void OnDestroy() => _back?.Dispose();

        private void Start()
        {
            _back.Enable();
            Refresh();
            ShowMain();
        }

        private void Update()
        {
            if (_back.WasPressedThisFrame() && (ConfirmVisible || SettingsVisible)) ShowMain();

            // Keep something selected so keyboard/gamepad always work, even after a mouse click on empty space.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) SelectDefault();
        }

        /// <summary>Re-reads the save (the menu may be shown again after a save was deleted).</summary>
        public void Refresh()
        {
            _save = SaveSystem.Read();
            ContinueButton.interactable = _save != null;
            _saveInfo.text = _save != null ? Describe(_save) : "";
        }

        // ---------------------------------------------------------------- actions

        public void OnContinue()
        {
            if (_save != null) GameFlow.Continue();
        }

        public void OnNewGame()
        {
            if (_save != null) ShowConfirm();
            else GameFlow.NewGame();
        }

        public void OnSettings()
        {
            _main.gameObject.SetActive(false);
            _confirm.gameObject.SetActive(false);
            _settings.Root.gameObject.SetActive(true);
            _saveInfo.gameObject.SetActive(false);
            Select(_settings.First);
        }

        public void ShowMain()
        {
            _confirm.gameObject.SetActive(false);
            _settings.Root.gameObject.SetActive(false);
            _main.gameObject.SetActive(true);
            _saveInfo.gameObject.SetActive(true);
            SelectDefault();
        }

        private void ShowConfirm()
        {
            _main.gameObject.SetActive(false);
            _confirm.gameObject.SetActive(true);
            _saveInfo.gameObject.SetActive(false);
            Select(CancelNewGameButton); // the safe choice is the default
        }

        private void SelectDefault()
        {
            if (ConfirmVisible) Select(CancelNewGameButton);
            else if (SettingsVisible) Select(_settings.First);
            else Select(ContinueButton.interactable ? ContinueButton : NewGameButton);
        }

        private static void Select(Selectable s)
        {
            if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        private static string Describe(SaveData s)
        {
            string where = string.IsNullOrEmpty(s.roomName) ? "" : s.roomName;
            string when = "";
            if (DateTime.TryParse(s.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t))
            {
                TimeSpan ago = DateTime.UtcNow - t.ToUniversalTime();
                when = ago.TotalMinutes < 1 ? "just now"
                    : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
                    : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago"
                    : t.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.InvariantCulture);
            }
            return string.Join("  ·  ", new[] { where, when }).Trim(' ', '·');
        }

        // ---------------------------------------------------------------- layout

        private void Build()
        {
            Camera cam = Camera.main;
            Canvas canvas = MenuWidgets.Canvas(transform, "TitleCanvas", 10, cam);
            Transform root = canvas.transform;

            Image bg = MenuWidgets.Panel(root, "Background", MenuWidgets.Background);
            MenuWidgets.Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);
            bg.raycastTarget = false;

            // A warm strip of "light" across the bottom third, echoing the facility's lamps.
            Image glow = MenuWidgets.Panel(root, "Glow", new Color(0.55f, 0.18f, 0.12f, 0.06f));
            MenuWidgets.Anchor(glow.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.2f), Vector2.zero, Vector2.zero);
            glow.raycastTarget = false;

            Text title = MenuWidgets.Label(root, "Title", "N U L L   R E C O R D", 64, MenuWidgets.TextColor);
            MenuWidgets.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(120f, -210f), new Vector2(0f, -120f));
            Image rule = MenuWidgets.Panel(root, "Rule", MenuWidgets.Accent);
            MenuWidgets.Anchor(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f, -222f), new Vector2(304f, -219f));
            Text sub = MenuWidgets.Label(root, "Subtitle", "SUBJECT FILE NOT FOUND", 18, MenuWidgets.Muted);
            MenuWidgets.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(124f, -262f), new Vector2(0f, -232f));

            // Main menu.
            _main = MenuWidgets.Rect(root, "Main");
            MenuWidgets.Anchor(_main, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(106f, 110f), new Vector2(560f, 390f));
            ContinueButton = MenuWidgets.Button(_main, "Continue", "Continue", OnContinue);
            NewGameButton = MenuWidgets.Button(_main, "NewGame", "New Game", OnNewGame);
            SettingsButton = MenuWidgets.Button(_main, "Settings", "Settings", OnSettings);
            QuitButton = MenuWidgets.Button(_main, "Quit", "Quit", GameFlow.QuitGame);
            MenuWidgets.Stack(_main, 50f, 8f);
            MenuWidgets.ChainVertical(ContinueButton, NewGameButton, SettingsButton, QuitButton);

            _saveInfo = MenuWidgets.Label(root, "SaveInfo", "", 18, MenuWidgets.Muted);
            MenuWidgets.Anchor(_saveInfo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(330f, 342f), new Vector2(760f, 372f));

            // New-game confirmation.
            _confirm = MenuWidgets.Rect(root, "ConfirmNewGame");
            MenuWidgets.Anchor(_confirm, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(106f, 110f), new Vector2(760f, 390f));
            Text q = MenuWidgets.Label(_confirm, "Question", "Start a new game?\nYour current progress will be erased.", 24, MenuWidgets.TextColor, TextAnchor.UpperLeft);
            MenuWidgets.Anchor(q.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -80f), new Vector2(0f, 0f));
            ConfirmNewGameButton = MenuWidgets.Button(_confirm, "Erase", "Erase save and start", GameFlow.NewGame);
            MenuWidgets.Anchor((RectTransform)ConfirmNewGameButton.transform, new Vector2(0f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, -150f), new Vector2(0f, -100f));
            ConfirmNewGameButton.GetComponentInChildren<Text>().color = MenuWidgets.Danger;
            CancelNewGameButton = MenuWidgets.Button(_confirm, "Cancel", "Cancel", ShowMain);
            MenuWidgets.Anchor((RectTransform)CancelNewGameButton.transform, new Vector2(0f, 1f), new Vector2(0.75f, 1f), new Vector2(0f, -208f), new Vector2(0f, -158f));
            MenuWidgets.ChainVertical(ConfirmNewGameButton, CancelNewGameButton);
            _confirm.gameObject.SetActive(false);

            _settings = new SettingsPanel(root, ShowMain);

            Text version = MenuWidgets.Label(root, "Version", $"prototype {Application.version}", 14, MenuWidgets.Muted, TextAnchor.LowerRight);
            MenuWidgets.Anchor(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-320f, 18f), new Vector2(-24f, 44f));
        }
    }
}
