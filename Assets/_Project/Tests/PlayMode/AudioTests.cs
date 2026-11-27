using System.Collections;
using Game.Audio;
using Game.Combat;
using Game.Core;
using Game.UI;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Sounds fire on the right events and ambience follows the player. Checks the AudioManager's
    /// counters rather than actual output, since a batch-mode editor may have no audio device.
    /// </summary>
    public class AudioTests : GreyboxTestBase
    {
        private static AudioManager Audio => AudioManager.Instance;

        [UnityTest]
        public IEnumerator Walking_PlaysFootsteps_JumpAndLanding()
        {
            Assert.IsNotNull(Audio, "AudioManager missing (rebuild scenes)");
            Press(Key.D);
            yield return Wait(1.2f);
            Release(Key.D);
            Assert.GreaterOrEqual(Audio.PlayCount(SoundId.Footstep), 4, "about one step per 0.6 m");

            yield return Wait(0.2f);
            yield return Tap(Key.Space);
            yield return Until(() => Audio.PlayCount(SoundId.Land) > 0, 2f, "landing sound");
            Assert.AreEqual(1, Audio.PlayCount(SoundId.Jump));
        }

        [UnityTest]
        public IEnumerator Ladder_Clanks()
        {
            _motor.Teleport(new Vector3(22.5f, 0.05f, 0f));
            yield return Until(() => _motor.State == Player.MotorState.Grounded, 1f, "at ladder");
            Press(Key.W);
            yield return Until(() => Audio.PlayCount(SoundId.LadderClank) >= 3, 3f, "rung clanks");
            Release(Key.W);
        }

        [UnityTest]
        public IEnumerator Door_DeniedThenOpens_AndPickupChimes()
        {
            _motor.Teleport(new Vector3(38.9f, 0.05f, 0f));
            yield return Until(() => _interactor.Current is Door, 2f, "door prompt");
            yield return Tap(Key.E);
            Assert.AreEqual(1, Audio.PlayCount(SoundId.DoorDenied));

            _motor.Teleport(new Vector3(36f, 0.05f, 0f));
            yield return Until(() => _interactor.Current is ItemPickup, 2f, "keycard prompt");
            yield return Tap(Key.E);
            Assert.AreEqual(1, Audio.PlayCount(SoundId.Pickup));

            _motor.Teleport(new Vector3(38.9f, 0.05f, 0f));
            yield return Until(() => _interactor.Current is Door, 2f, "door prompt again");
            yield return Tap(Key.E);
            Assert.AreEqual(1, Audio.PlayCount(SoundId.DoorOpen));
        }

        [UnityTest]
        public IEnumerator Ambience_FollowsTheRoom()
        {
            yield return Until(() => Audio.CurrentAmbience == nameof(SoundId.AmbHolding), 1f, "Holding hum at the start");
            _motor.Teleport(new Vector3(26f, 0.05f, 0f));
            yield return Until(() => Audio.CurrentAmbience == nameof(SoundId.AmbMaintenance), 1f, "Maintenance rumble");
            _motor.Teleport(new Vector3(46f, 0.05f, 0f));
            yield return Until(() => Audio.CurrentAmbience == nameof(SoundId.AmbSecurity), 1f, "Security drone");

            AmbientEmitter steam = Object.FindAnyObjectByType<AmbientEmitter>();
            Assert.IsNotNull(steam, "steam vent has a sound");
            Assert.AreEqual(SoundId.SteamLoop, steam.Sound);
            Assert.IsTrue(steam.Source.loop);
            Assert.IsNotNull(steam.Source.clip);
        }

        [UnityTest]
        public IEnumerator Combat_LoopPlaysWhileInAHostileRoom()
        {
            Enemy.Passive = false;
            _motor.Teleport(new Vector3(38.5f, 0.05f, 0f));
            yield return Wait(0.5f);
            float musicBefore = Audio.MusicVolume;
            Assert.Greater(musicBefore, 0f, "exploration music is up before the fight");
            Assert.IsFalse(Audio.CombatMusicOn);
            _motor.Teleport(new Vector3(43.0f, 0.05f, 0f));
            yield return Until(() => Audio.CombatMusicOn, 1f, "combat loop on inside Security");
            yield return Wait(1.2f);
            Assert.Less(Audio.MusicVolume, musicBefore * 0.5f, "music ducks under the combat loop");
            _motor.Teleport(new Vector3(38.5f, 0.05f, 0f));
            yield return Until(() => !Audio.CombatMusicOn, 1f, "combat loop off after leaving");
            Enemy.Passive = true;
        }

        [UnityTest]
        public IEnumerator Pause_FreezesWorldAudio_AndMenusClick()
        {
            yield return Tap(Key.Escape);
            Assert.IsTrue(AudioListener.pause, "world audio pauses with the game");
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            int clicks = Audio.PlayCount(SoundId.UiConfirm);
            pause.ResumeButton.onClick.Invoke();
            Assert.AreEqual(clicks + 1, Audio.PlayCount(SoundId.UiConfirm), "buttons click");
            Assert.IsFalse(AudioListener.pause);
        }

        [UnityTest]
        public IEnumerator Title_HasAmbienceAndClicks()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(Audio, "title has an AudioManager");
            yield return Until(() => Audio.CurrentAmbience == nameof(SoundId.AmbSecurity), 1f, "title drone");
            TitleMenu title = Object.FindAnyObjectByType<TitleMenu>();
            title.SettingsButton.onClick.Invoke();
            Assert.AreEqual(1, Audio.PlayCount(SoundId.UiConfirm));
            float before = GameSettings.EffectsVolume;
            title.Settings.Effects.value = 0.25f;
            Assert.That(AudioManager.EffectsGain, Is.EqualTo(0.25f).Within(0.01f), "effects slider drives the gain");
            GameSettings.EffectsVolume = before;
        }

        [UnityTest]
        public IEnumerator Music_FollowsItsSlider()
        {
            float before = GameSettings.MusicVolume;
            GameSettings.MusicVolume = 1f;
            yield return null;
            float full = Audio.MusicVolume;
            Assert.Greater(full, 0f, "music bed playing in game");
            GameSettings.MusicVolume = 0.5f;
            yield return null;
            Assert.That(Audio.MusicVolume, Is.EqualTo(full * 0.5f).Within(0.01f), "music slider scales it");
            GameSettings.MusicVolume = before;
        }
    }
}
