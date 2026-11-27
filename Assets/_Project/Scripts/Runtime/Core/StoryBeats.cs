using System.Collections;
using Game.Combat;
using Game.Dialogue;
using Game.Player;
using Game.Save;
using Game.UI;
using Game.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Structural story beats that don't depend on the owner's open story decisions (all wording is PLACEHOLDER, see GDD):
    /// an opening card on a new game, a one-line objective that follows progress, and an end card past the freight shutter.
    /// None of them takes control away except the end card, which offers a way back.
    /// </summary>
    public class StoryBeats : MonoBehaviour
    {
        public const string ShutterFlag = "shutter_freight";
        /// <summary>Beyond the freight shutter in the Specimen Hall (world X, and below this Y = region 2).</summary>
        public const float EndX = 153.4f, Region2BelowY = -20f;

        /// <summary>Tests turn the opening card off so screenshots aren't covered.</summary>
        public static bool SkipIntro { get; set; }

        private PlayerInventory _inv;
        private PlayerMotor _motor;
        private CanvasGroup _introGroup;
        private Text _objective;
        private GameObject _endCard;
        private Text _endStats;
        private bool _endShown;
        private bool _paResearch;

        public bool EndCardVisible => _endCard != null && _endCard.activeSelf;
        public string ObjectiveText => _objective != null ? _objective.text : "";
        public bool IntroPlaying { get; private set; }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _inv = p.GetComponent<PlayerInventory>();
                _motor = p.GetComponent<PlayerMotor>();
            }
            Build();
            // Only after New Game from the title (a loaded save or a test scene skips it). Nothing is saved for it.
            if (GameFlow.StartedNewGame && !SkipIntro) StartCoroutine(Intro());
            GameFlow.StartedNewGame = false;
        }

        /// <summary>Intercom lines over the opening card. PLACEHOLDER wording.</summary>
        public static readonly string[] IntroPa =
        {
            "Holding 03: door fault. Occupant, remain where you are.",
            "Correction. Holding 03 is unassigned. Disregard.",
        };

        private IEnumerator Intro()
        {
            IntroPlaying = true;
            _introGroup.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { _introGroup.alpha = t; yield return null; }
            _introGroup.alpha = 1f;
            foreach (string line in IntroPa)
            {
                DialogueUI.Ensure().Bark("pa", line);
                for (float t = 0f; t < 4.2f; t += Time.unscaledDeltaTime) yield return null;
            }
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime) { _introGroup.alpha = t; yield return null; }
            _introGroup.gameObject.SetActive(false);
            IntroPlaying = false;
        }

        private void Update()
        {
            if (_inv == null || _objective == null) return;
            bool busy = EncounterController.Active != null || (DialogueUI.Instance != null && (DialogueUI.Instance.IsOpen || DialogueUI.Instance.BarkVisible)) || _endShown || Time.timeScale <= 0f;
            _objective.text = busy ? "" : "> " + Objective();

            Vector3 pos = _inv.transform.position;
            if (!_paResearch && pos.y < Region2BelowY && !busy)
            {
                _paResearch = true;
                DialogueUI.Ensure().Bark("pa", "Freight lift in use. Research Wing staff, remain at your stations."); // PLACEHOLDER
            }
            if (!_endShown && _inv.HasFlag(ShutterFlag) && pos.x > EndX && pos.y < Region2BelowY) ShowEnd();
            if (_endShown && !EndCardVisible && pos.x < EndX - 1.5f) _endShown = false; // walked back: the card can show again
        }

        /// <summary>What to do next, from progress so far. PLACEHOLDER wording.</summary>
        public string Objective()
        {
            if (_inv.KeycardLevel < 2) return "Find a Level 2 keycard";
            if (!_inv.HasTool(LaserGrid.SpooferTool)) return "Search Records for a way past the laser grid";
            if (_inv.transform.position.y > Region2BelowY) return "Ride the freight lift up (Server Hall)";
            if (!_inv.HasTool(RemoteConsole.OverrideTool)) return "Find a way across the pit in the Labs";
            if (!_inv.HasFlag(ShutterFlag)) return "Open the freight shutter";
            return "Go through the freight shutter";
        }

        private void ShowEnd()
        {
            _endShown = true;
            int fights = 0;
            foreach (string f in _inv.Flags) if (f.StartsWith("encounter_") && f.EndsWith("_cleared")) fights++;
            int logs = 0;
            foreach (string f in _inv.Flags) if (f.StartsWith(LogTerminal.FlagPrefix)) logs++;
            _endStats.text = $"Fights won: {fights} of 3     Logs: {logs} of {LogTerminal.Total}     Level {Progression.Level(_inv.Xp)}     Scrap: {_inv.Scrap}";
            _endCard.SetActive(true);
            if (_motor != null) _motor.ControlEnabled = false;
            SaveManager.Instance?.SaveNow("end");
        }

        public void KeepExploring()
        {
            _endCard.SetActive(false);
            if (_motor != null) _motor.ControlEnabled = true;
        }

        private void Build()
        {
            Canvas canvas = MenuWidgets.Canvas(transform, "StoryCanvas", 60, Camera.main);
            Transform r = canvas.transform;

            // Objective, top-left under the safe margin.
            _objective = MenuWidgets.Label(r, "Objective", "", 18, new Color(0.85f, 0.9f, 0.95f, 0.85f), TextAnchor.UpperLeft);
            MenuWidgets.Anchor(_objective.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(24f, -74f), new Vector2(0f, -44f));
            _objective.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);

            // Opening card.
            Image scrim = MenuWidgets.Panel(r, "IntroCard", new Color(0f, 0f, 0f, 0.55f));
            MenuWidgets.Anchor(scrim.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 10f), new Vector2(0f, 170f));
            scrim.raycastTarget = false;
            _introGroup = scrim.gameObject.AddComponent<CanvasGroup>();
            _introGroup.blocksRaycasts = false;
            Text card = MenuWidgets.Label(scrim.transform, "Text",
                "HOLDING 03\n<size=24><color=#FFC98A>Find a way up and out.</color></size>",
                46, MenuWidgets.TextColor, TextAnchor.MiddleCenter);
            card.lineSpacing = 1.15f;
            MenuWidgets.Stretch(card.rectTransform, Vector2.zero, Vector2.zero);
            scrim.gameObject.SetActive(false);

            // End card.
            Image end = MenuWidgets.Panel(r, "EndCard", new Color(0.02f, 0.025f, 0.03f, 0.9f));
            MenuWidgets.Stretch(end.rectTransform, Vector2.zero, Vector2.zero);
            _endCard = end.gameObject;
            Text title = MenuWidgets.Label(end.transform, "Title", "SOMEONE WAS WAITING", 52, MenuWidgets.TextColor, TextAnchor.MiddleCenter);
            MenuWidgets.Anchor(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 120f), new Vector2(0f, 200f));
            Text sub = MenuWidgets.Label(end.transform, "Sub", "Past the shutter, a figure stands in the dark. It knows your number.  (End of the slice.)", 22, MenuWidgets.Muted, TextAnchor.MiddleCenter);
            MenuWidgets.Anchor(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 80f), new Vector2(0f, 116f));
            _endStats = MenuWidgets.Label(end.transform, "Stats", "", 22, MenuWidgets.Accent, TextAnchor.MiddleCenter);
            MenuWidgets.Anchor(_endStats.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 30f), new Vector2(0f, 66f));
            RectTransform buttons = MenuWidgets.Rect(end.transform, "Buttons");
            MenuWidgets.Anchor(buttons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, -130f), new Vector2(170f, -10f));
            Button title1 = MenuWidgets.Button(buttons, "BackToTitle", "Back to Title", GameFlow.QuitToTitle);
            Button keep = MenuWidgets.Button(buttons, "KeepExploring", "Keep Exploring", KeepExploring);
            MenuWidgets.Stack(buttons, 50f, 8f);
            MenuWidgets.ChainVertical(title1, keep);
            _endCard.SetActive(false);
        }
    }
}
