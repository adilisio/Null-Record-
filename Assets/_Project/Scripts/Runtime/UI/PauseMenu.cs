using Game.Core;
using Game.Player;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Esc / gamepad Start pauses: the game freezes and player input is switched off.
    /// Resume, Settings, Quit to Title (autosaves first when it's safe), Quit Game.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _main;
        private SettingsPanel _settings;
        private Text _where;
        private Text _note;
        private InputAction _pause;
        private InputActionMap _playerMap;
        private PlayerMotor _motor;

        public bool IsPaused { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button QuitToTitleButton { get; private set; }
        public Button QuitGameButton { get; private set; }
        public Button RetreatButton { get; private set; }
        public SettingsPanel Settings => _settings;

        private void Awake()
        {
            _pause = new InputAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");
        }

        private void Start()
        {
            GameSettings.Apply();
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _motor = p.GetComponent<PlayerMotor>();
                _playerMap = p.GetComponent<PlayerInputReader>()?.Map;
            }
            MenuWidgets.EnsureEventSystem();
            Build();
            _pause.Enable();
        }

        private void OnDestroy()
        {
            _pause?.Dispose();
            if (IsPaused) Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Update()
        {
            if (!_pause.WasPressedThisFrame()) return;
            if (!IsPaused) Pause();
            else if (_settings.Root.gameObject.activeSelf) ShowMain();
            else Resume();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true; // world sounds freeze; menu clicks ignore the pause
            _playerMap?.Disable();
            if (_root.GetComponent<Canvas>().worldCamera == null) _root.GetComponent<Canvas>().worldCamera = Camera.main;
            _root.gameObject.SetActive(true);
            SaveManager saves = SaveManager.Instance;
            _where.text = saves != null && !string.IsNullOrEmpty(saves.CurrentRoomName) ? saves.CurrentRoomName.ToUpperInvariant() : "";
            RetreatButton.gameObject.SetActive(Game.Combat.EncounterController.Active != null);
            MenuWidgets.ChainVertical(ResumeButton, RetreatButton, SettingsButton, QuitToTitleButton, QuitGameButton);
            _note.text = _motor != null && _motor.ExternallyControlled
                ? "Quitting now returns you to just before this fight."
                : "Progress is saved automatically.";
            ShowMain();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _playerMap?.Enable();
            _root.gameObject.SetActive(false);
        }

        public void QuitToTitle()
        {
            QuitToTitleSave();
            IsPaused = false;
            _playerMap?.Enable();
            GameFlow.QuitToTitle();
        }

        private void ShowMain()
        {
            _settings.Root.gameObject.SetActive(false);
            _main.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ResumeButton.gameObject);
        }

        private void ShowSettings()
        {
            _main.gameObject.SetActive(false);
            _settings.Root.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_settings.First.gameObject);
        }

        private void Build()
        {
            Canvas canvas = MenuWidgets.Canvas(transform, "PauseCanvas", 80, Camera.main);
            _root = (RectTransform)canvas.transform;

            Image scrim = MenuWidgets.Panel(_root, "Scrim", MenuWidgets.Scrim);
            MenuWidgets.Stretch(scrim.rectTransform, Vector2.zero, Vector2.zero);

            Text title = MenuWidgets.Label(_root, "Title", "PAUSED", 48, MenuWidgets.TextColor);
            MenuWidgets.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(120f, -190f), new Vector2(0f, -120f));
            _where = MenuWidgets.Label(_root, "Room", "", 18, MenuWidgets.Accent);
            MenuWidgets.Anchor(_where.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(124f, -226f), new Vector2(0f, -196f));

            _main = MenuWidgets.Rect(_root, "Main");
            MenuWidgets.Anchor(_main, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(106f, 130f), new Vector2(560f, 400f));
            ResumeButton = MenuWidgets.Button(_main, "Resume", "Resume", Resume);
            RetreatButton = MenuWidgets.Button(_main, "Retreat", "Retreat from Fight", () =>
            {
                Resume();
                Game.Combat.EncounterController.Active?.Retreat();
            });
            SettingsButton = MenuWidgets.Button(_main, "Settings", "Settings", ShowSettings);
            QuitToTitleButton = MenuWidgets.Button(_main, "QuitToTitle", "Quit to Title", QuitToTitle);
            QuitGameButton = MenuWidgets.Button(_main, "QuitGame", "Quit Game", () =>
            {
                QuitToTitleSave();
                GameFlow.QuitGame();
            });
            if (Application.platform == RuntimePlatform.WebGLPlayer) QuitGameButton.gameObject.SetActive(false);
            MenuWidgets.Stack(_main, 50f, 8f);
            MenuWidgets.ChainVertical(ResumeButton, RetreatButton, SettingsButton, QuitToTitleButton, QuitGameButton);

            _note = MenuWidgets.Label(_root, "Note", "", 16, MenuWidgets.Muted);
            MenuWidgets.Anchor(_note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(124f, 80f), new Vector2(0f, 110f));

            _settings = new SettingsPanel(_root, ShowMain);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Saves on the way out if the player is somewhere safe; otherwise the last autosave stands.</summary>
        private void QuitToTitleSave()
        {
            bool safe = _motor != null && !_motor.ExternallyControlled && _motor.State == MotorState.Grounded;
            if (safe && SaveManager.Instance != null) SaveManager.Instance.SaveNow("quit");
        }
    }
}
