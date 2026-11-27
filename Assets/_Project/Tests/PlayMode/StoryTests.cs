using System.Collections;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.UI;
using Game.World;
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
        public IEnumerator Logs_EachTerminalIsReachable_AndReadingSetsItsFlag()
        {
            LogTerminal[] terms = Object.FindObjectsByType<LogTerminal>();
            Assert.AreEqual(LogTerminal.Total, terms.Length, "log terminals placed (rebuild scenes)");
            bool shot = false;
            foreach (LogTerminal term in terms)
            {
                _motor.Teleport(term.transform.position + new Vector3(0f, 0.05f, 0f));
                yield return Until(() => _interactor.Current == term, 2f, $"prompt at {term.LogId}");
                StringAssert.StartsWith("Read:", term.GetPrompt(_interactor));
                term.Interact(_interactor);
                yield return Until(() => DialogueUI.Instance != null && DialogueUI.Instance.IsOpen, 2f, $"{term.LogId} opens");
                Assert.IsFalse(string.IsNullOrEmpty(DialogueUI.Instance.CurrentLine), "log has text");
                if (!shot)
                {
                    shot = true;
                    yield return Wait(0.3f);
                    yield return Shot("28_log_terminal");
                }
                for (int i = 0; i < 10 && DialogueUI.Instance.IsOpen; i++)
                {
                    yield return null;
                    DialogueUI.Instance.Confirm();
                }
                Assert.IsFalse(DialogueUI.Instance.IsOpen, "log closes");
                Assert.IsTrue(_inventory.HasFlag(term.Flag), $"{term.Flag} set");
                StringAssert.StartsWith("Read again:", term.GetPrompt(_interactor));
                yield return null;
            }
        }

    }
}
