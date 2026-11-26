using System;
using System.Collections;
using System.Collections.Generic;
using Game.Player;
using Game.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Dialogue
{
    /// <summary>
    /// Text box with a speaker portrait and choices, plus small non-blocking "barks" for banter.
    /// Built at runtime (no prefab yet). Uses a Screen Space - Camera canvas so it renders with the game camera.
    ///
    /// Controls: Jump / Interact / Attack = continue or pick; Move up/down = highlight a choice.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        [SerializeField] private float barkSeconds = 4f;

        private DialogueRunner _runner;
        private DialogueScript _script;
        private PlayerInteractor _player;
        private Action _onDone;
        private int _selected;
        private int _blockedUntilFrame;
        private bool _moveHeld;
        private float _barkUntil;

        private Canvas _canvas;
        private GameObject _box;
        private Image _portrait;
        private Text _portraitInitial;
        private Text _name;
        private Text _body;
        private Text _hint;
        private readonly List<Text> _choiceTexts = new List<Text>();
        private GameObject _bark;
        private Image _barkPortrait;
        private Text _barkText;

        private InputAction _aMove, _aJump, _aInteract, _aAttack;

        public bool IsOpen => _runner != null;
        public DialogueRunner Runner => _runner;
        public int Selected => _selected;
        public bool BarkVisible => _bark != null && _bark.activeSelf;
        public string BarkText => _barkText != null ? _barkText.text : "";

        /// <summary>Raised with the script id when a conversation closes.</summary>
        public event Action<string> Finished;

        public static DialogueUI Ensure()
        {
            if (Instance == null) new GameObject("DialogueUI").AddComponent<DialogueUI>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- public API

        public void Play(DialogueScript script, string startNode, PlayerInteractor player, Action onDone = null)
        {
            if (IsOpen || script == null || player == null) return;
            _script = script;
            _player = player;
            _onDone = onDone;
            PlayerInventory inv = player.Inventory;
            _runner = new DialogueRunner(script, f => inv != null && inv.HasFlag(f), f => inv?.SetFlag(f));
            _runner.Start(startNode);
            if (_runner.IsFinished)
            {
                _runner = null;
                return;
            }

            BindInput(player);
            if (player.Motor != null) player.Motor.ControlEnabled = false;
            _blockedUntilFrame = Time.frameCount + 1; // the press that opened it must not also advance it
            _moveHeld = true;
            _selected = 0;
            HideBark();
            AttachCamera();
            _box.SetActive(true);
            Refresh();
            GameAudio.Play(SoundId.DialogueBlip);
        }

        /// <summary>Continue, or pick the highlighted choice.</summary>
        public void Confirm()
        {
            if (!IsOpen) return;
            if (_runner.Choices.Count > 0) _runner.Choose(_selected);
            else _runner.Advance();
            _selected = 0;
            if (_runner.IsFinished) Close();
            else
            {
                Refresh();
                GameAudio.Play(SoundId.DialogueBlip);
            }
        }

        public void MoveSelection(int delta)
        {
            if (!IsOpen || _runner.Choices.Count == 0) return;
            _selected = (_selected + delta + _runner.Choices.Count) % _runner.Choices.Count;
            Refresh();
            GameAudio.Play(SoundId.UiMove);
        }

        /// <summary>A one-line remark that doesn't stop play.</summary>
        public void Bark(string speakerKey, string text)
        {
            if (IsOpen) return;
            AttachCamera();
            Speakers.Info info = Speakers.Get(speakerKey);
            Sprite face = Speakers.Portrait(speakerKey);
            _barkPortrait.sprite = face;
            _barkPortrait.color = face != null ? Color.white : info.Color;
            _barkText.text = $"<b>{info.Name}</b>  {text}";
            _bark.SetActive(true);
            GameAudio.Play(SoundId.DialogueBlip);
            _barkUntil = Time.time + barkSeconds;
        }

        // ---------------------------------------------------------------- frame loop

        private void Update()
        {
            if (BarkVisible && Time.time > _barkUntil) HideBark();
            if (!IsOpen || Time.frameCount <= _blockedUntilFrame) return;

            Vector2 mv = _aMove != null ? _aMove.ReadValue<Vector2>() : Vector2.zero;
            if (Mathf.Abs(mv.y) < 0.4f) _moveHeld = false;
            else if (!_moveHeld)
            {
                _moveHeld = true;
                MoveSelection(mv.y > 0 ? -1 : 1);
            }

            if (Pressed(_aJump) || Pressed(_aInteract) || Pressed(_aAttack) || TappedOffUi()) Confirm();
        }

        /// <summary>The line on screen right now (tests).</summary>
        public string CurrentLine => _body != null ? _body.text : "";

        private static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();

        /// <summary>A tap anywhere that isn't an on-screen button (those already send their own input).</summary>
        private static bool TappedOffUi()
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null || !ts.primaryTouch.press.wasPressedThisFrame) return false;
            int id = ts.primaryTouch.touchId.ReadValue();
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(id);
        }

        private void Close()
        {
            _box.SetActive(false);
            string id = _script != null ? _script.id : "";
            _runner = null;
            _script = null;
            StartCoroutine(ReturnControlNextFrame(_player));
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
            Finished?.Invoke(id);
        }

        // Waiting a frame stops the closing press from also triggering the interactable again.
        private static IEnumerator ReturnControlNextFrame(PlayerInteractor player)
        {
            yield return null;
            if (player != null && player.Motor != null && !player.Motor.ExternallyControlled) player.Motor.ControlEnabled = true;
        }

        private void BindInput(PlayerInteractor player)
        {
            InputActionMap map = player.GetComponent<PlayerInputReader>()?.Map;
            if (map == null) return;
            _aMove = map.FindAction("Move");
            _aJump = map.FindAction("Jump");
            _aInteract = map.FindAction("Interact");
            _aAttack = map.FindAction("Attack");
        }

        // ---------------------------------------------------------------- view

        private void Refresh()
        {
            DialogueNode n = _runner.Current;
            Speakers.Info info = Speakers.Get(n.speaker);
            Sprite face = Speakers.Portrait(n.speaker);
            _portrait.sprite = face;
            _portrait.color = face != null ? Color.white : info.Color;
            _portraitInitial.text = face != null || string.IsNullOrEmpty(info.Name) ? "" : info.Name.Substring(0, 1);
            _name.text = info.Name;
            _body.text = n.text;

            IReadOnlyList<DialogueChoice> choices = _runner.Choices;
            for (int i = 0; i < _choiceTexts.Count; i++)
            {
                bool on = i < choices.Count;
                _choiceTexts[i].gameObject.SetActive(on);
                if (!on) continue;
                bool sel = i == _selected;
                _choiceTexts[i].text = (sel ? "> " : "   ") + choices[i].text;
                _choiceTexts[i].color = sel ? new Color(1f, 0.85f, 0.55f) : new Color(0.7f, 0.72f, 0.75f);
            }
            _hint.text = choices.Count > 0 ? Game.UI.InputHints.DialogueChoose : Game.UI.InputHints.DialogueContinue;
        }

        private void HideBark()
        {
            if (_bark != null) _bark.SetActive(false);
        }

        private void AttachCamera()
        {
            if (_canvas != null && _canvas.worldCamera == null) _canvas.worldCamera = Camera.main;
        }

        private void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("DialogueCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Main box along the bottom.
            _box = Panel(canvasGo.transform, "Box", new Color(0.04f, 0.05f, 0.06f, 0.92f));
            Place(_box, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 24f), new Vector2(-40f, 214f));

            GameObject portraitGo = Panel(_box.transform, "Portrait", Color.gray);
            _portrait = portraitGo.GetComponent<Image>();
            Place(portraitGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -75f), new Vector2(170f, 75f));
            _portraitInitial = Label(portraitGo.transform, "Initial", font, 72, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            Stretch(_portraitInitial.gameObject);

            _name = Label(_box.transform, "Name", font, 26, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.6f));
            _name.fontStyle = FontStyle.Bold;
            Place(_name.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(195f, -48f), new Vector2(-20f, -12f));

            _body = Label(_box.transform, "Body", font, 24, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.9f));
            Place(_body.gameObject, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(195f, -180f), new Vector2(-10f, -52f));

            for (int i = 0; i < 4; i++)
            {
                Text c = Label(_box.transform, $"Choice{i}", font, 22, TextAnchor.UpperLeft, Color.white);
                float top = -52f - i * 34f;
                Place(c.gameObject, new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(10f, top - 32f), new Vector2(-20f, top));
                _choiceTexts.Add(c);
            }

            _hint = Label(_box.transform, "Hint", font, 16, TextAnchor.LowerRight, new Color(0.6f, 0.62f, 0.65f));
            Place(_hint.gameObject, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(-16f, 32f));
            _box.SetActive(false);

            // Bark: small strip top-left.
            _bark = Panel(canvasGo.transform, "Bark", new Color(0.04f, 0.05f, 0.06f, 0.85f));
            Place(_bark, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -96f), new Vector2(664f, -24f));
            GameObject bp = Panel(_bark.transform, "Portrait", Color.gray);
            _barkPortrait = bp.GetComponent<Image>();
            Place(bp, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -28f), new Vector2(64f, 28f));
            _barkText = Label(_bark.transform, "Text", font, 20, TextAnchor.MiddleLeft, new Color(0.92f, 0.92f, 0.9f));
            _barkText.supportRichText = true;
            Place(_barkText.gameObject, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(76f, 6f), new Vector2(-12f, -6f));
            _bark.SetActive(false);
        }

        private static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text Label(Transform parent, string name, Font font, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Place(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void Stretch(GameObject go) => Place(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }
}
