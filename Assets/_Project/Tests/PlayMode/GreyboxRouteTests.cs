using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Player;
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
    /// Plays the Greybox_Intake route with a virtual keyboard, the way a player would,
    /// and saves screenshots to &lt;project&gt;/Screenshots. Rebuild the scene with
    /// "Null Record/Build Greybox Test Level" before running if the layout changed.
    /// </summary>
    [Timeout(90000)] // fail instead of hanging the run
    public class GreyboxRouteTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox_Intake.unity";

        private Keyboard _kb;
        private readonly HashSet<Key> _held = new HashSet<Key>();
        private readonly List<string> _messages = new List<string>();
        private readonly List<MotorState> _states = new List<MotorState>();

        private PlayerMotor _motor;
        private PlayerInventory _inventory;
        private PlayerInteractor _interactor;
        private PlayerRespawn _respawn;

        private float X => _motor.transform.position.x;
        private float Y => _motor.transform.position.y;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            yield return null;
        }

        private void OnMessage(string m)
        {
            _messages.Add(m);
            Debug.Log($"[RouteTest] message: {m}");
        }

        private void OnState(MotorState s) => _states.Add(s);

        // ---------------------------------------------------------------- tests

        [UnityTest]
        public IEnumerator FullRoute_StartToSecurity()
        {
            yield return Shot("01_start");

            // Room A: jump the crate.
            Press(Key.D);
            yield return Until(() => X >= 3.9f, 3f, "walk to crate");
            yield return Tap(Key.Space);
            yield return Until(() => X >= 7.2f && _motor.State == MotorState.Grounded, 3f, "clear the crate");
            yield return Shot("02_past_crate");

            // Ledge: jump into the tall block and climb.
            yield return Until(() => X >= 10.6f, 3f, "approach tall block");
            yield return Tap(Key.Space);
            yield return Until(() => _motor.State == MotorState.LedgeHang, 2f, "grab the ledge");
            yield return Shot("03_ledge_hang");
            Press(Key.W);
            yield return Until(() => _motor.State == MotorState.Grounded && Y > 2.3f, 2f, "climb onto block");
            Release(Key.W);
            yield return Shot("04_on_block");

            // Drop into room B; checkpoint at x 21.
            yield return Until(() => X >= 22.25f, 4f, "walk to ladder");
            Release(Key.D);
            Assert.That(_respawn.Checkpoint.x, Is.EqualTo(21f).Within(0.1f), "Checkpoint_B did not register (CharacterController trigger)");
            yield return Until(() => _motor.State == MotorState.Grounded && Mathf.Abs(_motor.Velocity.x) < 0.05f, 1f, "stop at ladder");

            // Ladder to catwalk.
            Press(Key.W);
            yield return Until(() => _motor.State == MotorState.Ladder, 1f, "grab ladder");
            yield return Wait(0.4f);
            yield return Shot("05_ladder");
            yield return Until(() => _motor.State == MotorState.Grounded && Y > 3.9f, 4f, "exit ladder onto catwalk");
            Release(Key.W);

            // Thermal Lining.
            Press(Key.D);
            yield return Until(() => X >= 27.2f, 3f, "walk to Thermal Lining");
            Release(Key.D);
            yield return Until(() => _interactor.Current is ItemPickup, 1f, "Thermal Lining prompt");
            yield return Shot("06_lining_prompt");
            yield return Tap(Key.E);
            yield return Until(() => _inventory.IsProtectedFrom(HazardType.Heat), 1f, "receive Thermal Lining");

            // Off the catwalk, through the steam, to the keycard.
            Press(Key.D);
            yield return Until(() => X >= 31.5f, 4f, "enter the steam");
            yield return Shot("07_in_steam");
            yield return Until(() => X >= 35.8f, 3f, "walk to keycard");
            Release(Key.D);
            Assert.IsFalse(_messages.Exists(m => m.Contains("Scalded")), "Died in steam despite Thermal Lining");
            yield return Until(() => _interactor.Current is ItemPickup, 1f, "keycard prompt");
            yield return Tap(Key.E);
            yield return Until(() => _inventory.KeycardLevel >= 2, 1f, "receive L2 keycard");

            // Door.
            Press(Key.D);
            yield return Until(() => X >= 38.9f, 3f, "walk to door");
            Release(Key.D);
            yield return Until(() => _interactor.Current is Door, 1f, "door prompt");
            yield return Shot("08_door");
            yield return Tap(Key.E);
            Door door = (Door)UnityEngine.Object.FindAnyObjectByType(typeof(Door));
            Assert.IsTrue(door.IsOpen, "Door did not open with L2 keycard");
            yield return Wait(0.6f);
            Press(Key.D);
            yield return Until(() => X >= 43f, 3f, "walk through the door");
            Release(Key.D);
            Assert.That(_respawn.Checkpoint.x, Is.EqualTo(41.5f).Within(0.1f), "Checkpoint_C did not register");
            yield return Wait(0.8f);
            yield return Shot("09_security");

            Debug.Log("[RouteTest] states: " + string.Join(",", _states));
        }

        [UnityTest]
        public IEnumerator Steam_WithoutProtection_RespawnsAtCheckpoint()
        {
            _motor.Teleport(new Vector3(28.5f, 0.05f, 0f));
            _respawn.SetCheckpoint(new Vector3(21f, 0.05f, 0f));
            yield return Until(() => _motor.State == MotorState.Grounded, 1f, "land near steam");

            Press(Key.D);
            yield return Until(() => _messages.Exists(m => m.Contains("Scalded")), 2f, "die in steam");
            Release(Key.D);
            yield return Until(() => Mathf.Abs(X - 21f) < 0.2f, 2f, "respawn at checkpoint");
            Assert.Less(X, 30f);
        }

        [UnityTest]
        public IEnumerator Door_WithoutKeycard_StaysLocked()
        {
            _motor.Teleport(new Vector3(38.9f, 0.05f, 0f));
            yield return Until(() => _interactor.Current is Door, 1f, "door prompt");
            yield return Tap(Key.E);
            Door door = (Door)UnityEngine.Object.FindAnyObjectByType(typeof(Door));
            Assert.IsFalse(door.IsOpen, "Door opened without a keycard");

            Press(Key.D);
            yield return Wait(1f);
            Release(Key.D);
            Assert.Less(X, 40f, "Walked through a locked door");
        }

        // ---------------------------------------------------------------- input helpers

        private void Press(Key k)
        {
            _held.Add(k);
            Apply();
        }

        private void Release(Key k)
        {
            _held.Remove(k);
            Apply();
        }

        private void Apply()
        {
            var keys = new Key[_held.Count];
            _held.CopyTo(keys);
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
        }

        private IEnumerator Tap(Key k)
        {
            Press(k);
            yield return Wait(0.15f);
            Release(k);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private IEnumerator Until(Func<bool> condition, float timeout, string step)
        {
            float end = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > end)
                {
                    string where = _motor != null
                        ? $" (pos {_motor.transform.position}, state {_motor.State}, vel {_motor.Velocity})"
                        : "";
                    yield return Shot("FAIL_" + step.Replace(' ', '_'));
                    Assert.Fail($"Timed out: {step}{where}");
                }
                yield return null;
            }
            Debug.Log($"[RouteTest] ok: {step} at x={X:0.00} y={Y:0.00} t={Time.time:0.00}");
        }

        // ---------------------------------------------------------------- screenshots

        private static IEnumerator Shot(string name)
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
