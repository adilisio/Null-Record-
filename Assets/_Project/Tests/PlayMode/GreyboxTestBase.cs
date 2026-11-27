using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Save;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Shared setup for PlayMode tests on Greybox_Intake: loads the scene, adds a virtual keyboard,
    /// and offers input, wait and screenshot helpers. Screenshots go to &lt;project&gt;/Screenshots.
    /// Rebuild the scene with "Null Record/Build Greybox Test Level" first if the layout changed.
    /// </summary>
    [Timeout(90000)] // fail instead of hanging the run
    public abstract class GreyboxTestBase
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox_Intake.unity";

        protected Keyboard _kb;
        protected readonly HashSet<Key> _held = new HashSet<Key>();
        protected readonly List<string> _messages = new List<string>();
        protected readonly List<MotorState> _states = new List<MotorState>();

        protected PlayerMotor _motor;
        protected PlayerInventory _inventory;
        protected PlayerInteractor _interactor;
        protected PlayerRespawn _respawn;

        protected float X => _motor.transform.position.x;
        protected float Y => _motor.transform.position.y;

        /// <summary>The hostile room in Security (the level has three).</summary>
        protected static EnemyRoom SecurityRoom => GameObject.Find("Encounter_Security").GetComponent<EnemyRoom>();
        protected static EnemyRoom ServersRoom => GameObject.Find("Encounter_Servers").GetComponent<EnemyRoom>();
        protected static EnemyRoom SpecimensRoom => GameObject.Find("Encounter_Specimens").GetComponent<EnemyRoom>();

        /// <summary>Dace, companion 1 (the level can hold more than one companion).</summary>
        protected static Game.Companions.CompanionNPC DaceNpc
        {
            get
            {
                foreach (Game.Companions.CompanionNPC n in UnityEngine.Object.FindObjectsByType<Game.Companions.CompanionNPC>(FindObjectsSortMode.None))
                {
                    if (n.name == "Dace") return n;
                }
                return null;
            }
        }

        /// <summary>Living hostiles standing between two x positions.</summary>
        protected static int IdleHostilesBetween(float x0, float x1)
        {
            int n = 0;
            foreach (Enemy e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e.gameObject.activeInHierarchy && !e.Dead && e.transform.position.x >= x0 && e.transform.position.x <= x1) n++;
            }
            return n;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Game.Core.StoryBeats.SkipIntro = true;
            Enemy.Passive = true; // route tests walk past hostiles; combat tests switch them on
            // The batch-mode editor never has focus; make keyboard input count anyway.
            // Work on a clone so the project's settings asset is never touched, and never
            // assign the original back: the Input System may have destroyed it by then.
            InputSettings s = InputSystem.settings;
            if (!s.name.EndsWith("(Clone)"))
            {
                s = UnityEngine.Object.Instantiate(s);
                InputSystem.settings = s;
            }
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = s; // re-assign so the change is applied
            Application.targetFrameRate = 60; // play at a real frame rate, not thousands of fps
            _kb = InputSystem.AddDevice<Keyboard>("RouteTestKeyboard");
            _held.Clear();

            _messages.Clear();
            _states.Clear();
            GameMessages.Posted += OnMessage;

            // Each test starts from a fresh game: saves go to a scratch folder that is wiped first.
            SaveSystem.DirectoryOverride = System.IO.Path.Combine(Application.temporaryCachePath, "NullRecordTestSaves");
            SaveSystem.Delete();

            yield return LoadScene();
        }

        /// <summary>(Re)loads the greybox scene and finds the player again. Reloading picks up the current save.</summary>
        protected IEnumerator LoadScene()
        {
            if (_motor != null) _motor.StateChanged -= OnState;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            GameObject player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(player, "Player not found in scene");
            _motor = player.GetComponent<PlayerMotor>();
            _inventory = player.GetComponent<PlayerInventory>();
            _interactor = player.GetComponent<PlayerInteractor>();
            _respawn = player.GetComponent<PlayerRespawn>();
            _motor.StateChanged += OnState;

            yield return Until(() => _motor.State == MotorState.Grounded, 2f, "player lands at spawn");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameMessages.Posted -= OnMessage;
            if (_motor != null) _motor.StateChanged -= OnState;
            if (_kb != null) InputSystem.RemoveDevice(_kb);
            Application.targetFrameRate = -1;
            Time.timeScale = 1f;
            SaveSystem.Delete();
            SaveSystem.DirectoryOverride = null;
            yield return null;
        }

        protected void OnMessage(string m)
        {
            _messages.Add(m);
            Debug.Log($"[RouteTest] message: {m}");
        }

        protected void OnState(MotorState s) => _states.Add(s);

        // ---------------------------------------------------------------- input helpers

        protected void Press(Key k)
        {
            _held.Add(k);
            Apply();
        }

        protected void Release(Key k)
        {
            _held.Remove(k);
            Apply();
        }

        protected void Apply()
        {
            var keys = new Key[_held.Count];
            _held.CopyTo(keys);
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
        }

        protected IEnumerator Tap(Key k)
        {
            Press(k);
            yield return Wait(0.15f);
            Release(k);
            yield return null;
        }

        protected static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        protected IEnumerator Until(Func<bool> condition, float timeout, string step)
        {
            float end = Time.unscaledTime + timeout; // unscaled: keeps working while paused
            while (!condition())
            {
                if (Time.unscaledTime > end)
                {
                    string where = _motor != null
                        ? $" (pos {_motor.transform.position}, state {_motor.State}, vel {_motor.Velocity})"
                        : "";
                    yield return Shot("FAIL_" + step.Replace(' ', '_'));
                    Assert.Fail($"Timed out: {step}{where}");
                }
                yield return null;
            }
            Debug.Log(_motor != null ? $"[RouteTest] ok: {step} at x={X:0.00} y={Y:0.00} t={Time.time:0.00}" : $"[RouteTest] ok: {step}");
        }

        // ---------------------------------------------------------------- screenshots

        protected static IEnumerator Shot(string name)
        {
            // Not WaitForEndOfFrame: it never resumes in batch mode. Rendering the camera manually works anywhere.
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) yield break;

            const int w = 1280, h = 720;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
