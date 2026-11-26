using System.Collections;
using Game.Combat;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Opening card, objective line, end card, and retreating from a fight.</summary>
    public class StoryTests : GreyboxTestBase
    {
        [UnityTest]
        public IEnumerator Objective_FollowsProgress_AndTheEndCardShowsPastTheShutter()
        {
            StoryBeats story = Object.FindAnyObjectByType<StoryBeats>();
            Assert.IsNotNull(story, "StoryBeats missing (rebuild scenes)");
            yield return null;
            StringAssert.Contains("keycard", story.ObjectiveText);
            _inventory.GrantKeycard(2);
            yield return null;
            StringAssert.Contains("laser grid", story.ObjectiveText);

            _inventory.SetFlag(StoryBeats.ShutterFlag);
            _motor.Teleport(new Vector3(154.5f, -29.9f, 0f));
            yield return Until(() => story.EndCardVisible, 2f, "end card past the shutter");
            Assert.IsFalse(_motor.ControlEnabled, "the end card holds the player");
            yield return Wait(0.3f);
            yield return Shot("27_end_card");
            story.KeepExploring();
            Assert.IsTrue(_motor.ControlEnabled, "keep exploring hands control back");
        }

        [UnityTest]
        public IEnumerator Retreat_FromThePauseMenu_ResetsTheFight()
        {
            EncounterController enc = SecurityEncounter;
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "combat starts");
            PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
            pause.Pause();
            yield return null;
            Assert.IsTrue(pause.RetreatButton.gameObject.activeSelf, "retreat offered during a fight");
            pause.RetreatButton.onClick.Invoke();
            yield return Until(() => enc.Status == EncounterStatus.Idle, 3f, "fight resets");
            Assert.IsFalse(_motor.ExternallyControlled, "control returns");
            Assert.AreEqual(1f, Time.timeScale, "unpaused");
            pause.Pause();
            yield return null;
            Assert.IsFalse(pause.RetreatButton.gameObject.activeSelf, "no retreat outside a fight");
            pause.Resume();
        }
    }
}
