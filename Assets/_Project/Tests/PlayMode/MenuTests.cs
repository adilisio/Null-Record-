using System.Collections;
using Game.Core;
using Game.Save;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Title screen (Continue / New Game / Settings) and the in-game pause menu.</summary>
    public class MenuTests : GreyboxTestBase
    {
        private TitleMenu _title;

        private IEnumerator OpenTitle()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
            _title = Object.FindAnyObjectByType<TitleMenu>();
            Assert.IsNotNull(_title, "TitleMenu missing (rebuild with Null Record > Build All Scenes)");
            yield return null;
        }

        private IEnumerator WaitForGameScene()
        {
            yield return Until(() => SceneManager.GetActiveScene().path == GameFlow.GameScene && GameObject.FindWithTag("Player") != null, 5f, "game scene loads");
            yield return null;
            GameObject player = GameObject.FindWithTag("Player");
            _motor = player.GetComponent<Player.PlayerMotor>();
            _inventory = player.GetComponent<Player.PlayerInventory>();
            _interactor = player.GetComponent<Player.PlayerInteractor>();
            _respawn = player.GetComponent<Player.PlayerRespawn>();
        }

        private static void WriteSave(Vector3 position, string roomName)
        {
            SaveSystem.Write(new SaveData
            {
                scene = GameFlow.GameScene,
                savedAtUtc = System.DateTime.UtcNow.AddMinutes(-5).ToString("o"),
                room = "Zone_B",
                roomName = roomName,
                position = position,
                checkpoint = position,
                keycardLevel = 2,
                flags = new[] { "taken_pickup_keycard_l2" },
            });
        }

        [UnityTest]
        public IEnumerator Title_WithoutSave_OffersOnlyNewGame()
        {
            yield return OpenTitle();
            Assert.IsFalse(_title.ContinueButton.interactable, "nothing to continue");
            Assert.AreEqual(_title.NewGameButton.gameObject, EventSystem.current.currentSelectedGameObject, "New Game is selected for keyboard/gamepad");
            yield return Wait(0.2f);
            yield return Shot("40_title_new");

            _title.NewGameButton.onClick.Invoke();
            Assert.IsFalse(_title.ConfirmVisible, "no confirmation needed without a save");
            yield return WaitForGameScene();
            Assert.That(X, Is.EqualTo(1f).Within(0.3f), "starts at the beginning");
        }

        [UnityTest]
        public IEnumerator Title_Continue_ResumesTheSave()
        {
            WriteSave(new Vector3(26f, 4.05f, 0f), "Maintenance");
            yield return OpenTitle();
            Assert.IsTrue(_title.ContinueButton.interactable);
            Assert.AreEqual(_title.ContinueButton.gameObject, EventSystem.current.currentSelectedGameObject, "Continue is the default");
            StringAssert.Contains("Maintenance", _title.SaveInfo);
            StringAssert.Contains("5 min ago", _title.SaveInfo);
            yield return Wait(0.2f);
            yield return Shot("41_title_continue");

            // Confirm with the keyboard, the way a player would.
            yield return Tap(Key.Enter);
            yield return WaitForGameScene();
            yield return Until(() => _motor.State == Player.MotorState.Grounded, 2f, "lands on the catwalk");
            Assert.That(X, Is.EqualTo(26f).Within(0.4f));
            Assert.That(Y, Is.EqualTo(4f).Within(0.3f));
            Assert.AreEqual(2, _inventory.KeycardLevel);
        }

        [UnityTest]
        public IEnumerator Title_NewGame_WithSave_AsksFirst()
        {
            WriteSave(new Vector3(26f, 4.05f, 0f), "Maintenance");
            yield return OpenTitle();
            _title.NewGameButton.onClick.Invoke();
            Assert.IsTrue(_title.ConfirmVisible, "asks before erasing");
            Assert.AreEqual(_title.CancelNewGameButton.gameObject, EventSystem.current.currentSelectedGameObject, "Cancel is the safe default");
            yield return Wait(0.2f);
            yield return Shot("42_title_confirm");

            yield return Tap(Key.Escape);
            Assert.IsFalse(_title.ConfirmVisible, "Esc backs out");
            Assert.IsTrue(SaveSystem.Exists, "backing out keeps the save");

            _title.NewGameButton.onClick.Invoke();
            _title.ConfirmNewGameButton.onClick.Invoke();
            yield return WaitForGameScene();
            Assert.IsFalse(SaveSystem.Exists && SaveSystem.Read().keycardLevel == 2, "old save is gone");
            Assert.That(X, Is.EqualTo(1f).Within(0.3f), "fresh start");
            Assert.AreEqual(0, _inventory.KeycardLevel);
        }

        [UnityTest]
        public IEnumerator Title_Settings_ChangeVolume()
        {
            float before = GameSettings.MasterVolume;
            yield return OpenTitle();
            _title.SettingsButton.onClick.Invoke();
            Assert.IsTrue(_title.SettingsVisible);
            _title.Settings.Volume.value = 0.3f;
            Assert.That(AudioListener.volume, Is.EqualTo(0.3f).Within(0.01f));
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(0.3f).Within(0.01f));
            yield return Wait(0.2f);
            yield return Shot("43_settings");
            _title.Settings.Back.onClick.Invoke();
            Assert.IsFalse(_title.SettingsVisible);
            GameSettings.MasterVolume = before;
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheGame_AndQuitsToTitleWithASave()
        {
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            Assert.IsNotNull(pause, "PauseMenu missing from the game scene");
            _motor.Teleport(new Vector3(24f, 0.05f, 0f));
            yield return Until(() => SaveManager.Instance.CurrentRoomName == "Maintenance" && _motor.State == Player.MotorState.Grounded, 2f, "in Maintenance");

            yield return Tap(Key.Escape);
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            float x0 = X;
            Press(Key.D);
            yield return Wait(0.4f);
            Release(Key.D);
            Assert.That(X, Is.EqualTo(x0).Within(0.01f), "no movement while paused");
            yield return Shot("44_pause");

            yield return Tap(Key.Escape);
            Assert.IsFalse(pause.IsPaused, "Esc resumes");
            Assert.AreEqual(1f, Time.timeScale);
            Press(Key.D);
            yield return Wait(0.3f);
            Release(Key.D);
            Assert.Greater(X, x0 + 0.3f, "input works again after resuming");

            yield return Until(() => _motor.State == Player.MotorState.Grounded, 1f, "settle");
            yield return Tap(Key.Escape);
            pause.QuitToTitleButton.onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().path == GameFlow.TitleScene, 5f, "back at the title");
            Assert.AreEqual(1f, Time.timeScale, "time runs again on the title");
            Assert.IsTrue(SaveSystem.Exists, "quitting saved");
            Assert.AreEqual("quit", SaveSystem.Read().reason);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<TitleMenu>().ContinueButton.interactable, "and the title offers Continue");
        }
    }
}
