using System.Collections;
using Game.Companions;
using Game.Dialogue;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Recruiting Dace, following, and banter.</summary>
    public class CompanionTests : GreyboxTestBase
    {
        private CompanionNPC _dace;
        private CompanionFollower _follower;
        private DialogueUI _ui;

        private IEnumerator FindDace()
        {
            _dace = DaceNpc;
            Assert.IsNotNull(_dace, "Dace missing from the scene (rebuild the greybox level)");
            _follower = _dace.GetComponent<CompanionFollower>();
            _ui = DialogueUI.Instance;
            Assert.IsNotNull(_ui, "DialogueUI missing from the scene");
            yield return null;
        }

        private IEnumerator TalkToDace()
        {
            _motor.Teleport(new Vector3(37.0f, 0.05f, 0f)); // nearer Dace than the keycard at 36
            yield return Until(() => _interactor.Current == _dace, 2f, "Dace prompt");
            yield return Tap(Key.E);
            yield return Until(() => _ui.IsOpen, 1f, "dialogue opens");
            Assert.IsFalse(_motor.ControlEnabled, "player can't walk off mid-conversation");
        }

        private IEnumerator Say(string expectedNode)
        {
            Assert.AreEqual(expectedNode, _ui.Runner.Current.id);
            yield return Tap(Key.Space);
        }

        [UnityTest]
        public IEnumerator Recruit_ByTalking_ThenFollows()
        {
            yield return FindDace();
            yield return TalkToDace();
            Assert.AreEqual("start", _ui.Runner.Current.id, "the E that opened it must not skip the first line");
            yield return Wait(0.1f);
            yield return Shot("20_dialogue");
            yield return Say("start");

            Assert.AreEqual("intro", _ui.Runner.Current.id);
            Assert.AreEqual(3, _ui.Runner.Choices.Count);
            yield return Tap(Key.S);
            yield return Tap(Key.S);
            Assert.AreEqual(2, _ui.Selected, "S moves the highlight down");
            yield return Tap(Key.W);
            yield return Tap(Key.W);
            Assert.AreEqual(0, _ui.Selected);
            yield return Wait(0.1f);
            yield return Shot("21_choices");
            yield return Tap(Key.Space); // "Who are you?"
            yield return Say("who");
            Assert.AreEqual("offer", _ui.Runner.Current.id);
            yield return Tap(Key.Space); // "Come with me."
            yield return Say("yes");

            yield return Until(() => !_ui.IsOpen, 1f, "dialogue closes");
            Assert.IsTrue(_inventory.HasFlag("companion_guard"));
            Assert.IsTrue(_dace.Recruited);
            Assert.AreEqual(_follower, CompanionFollower.Active);
            yield return Until(() => _motor.ControlEnabled, 1f, "control returns");
            Assert.AreNotEqual(_dace, _interactor.Current, "no prompt to talk again");

            // Walk back left (short of the steam); Dace keeps up behind.
            Press(Key.A);
            yield return Wait(0.6f);
            Release(Key.A);
            yield return Wait(0.8f);
            float gap = _follower.transform.position.x - X;
            Debug.Log($"[RouteTest] follow gap {gap:0.00}");
            Assert.That(gap, Is.InRange(0.3f, 3f), "Dace trails behind (to the right when walking left)");
            yield return Shot("22_following");
        }

        [UnityTest]
        public IEnumerator Decline_ThenRecruitLater()
        {
            yield return FindDace();
            yield return TalkToDace();
            yield return Say("start");
            yield return Tap(Key.S);
            yield return Tap(Key.S);
            yield return Tap(Key.Space); // say nothing
            yield return Say("silent");
            yield return Tap(Key.S);
            yield return Tap(Key.Space); // not yet
            yield return Say("no");
            yield return Until(() => !_ui.IsOpen, 1f, "dialogue closes");
            Assert.IsFalse(_dace.Recruited);
            Assert.IsNull(CompanionFollower.Active);

            yield return Until(() => _interactor.Current == _dace, 2f, "can talk again");
            yield return Tap(Key.E);
            yield return Until(() => _ui.IsOpen, 1f, "dialogue reopens");
            Assert.AreEqual("again", _ui.Runner.Current.id, "second conversation skips the introductions");
            yield return Tap(Key.Space); // "Come with me."
            yield return Say("yes");
            yield return Until(() => _dace.Recruited, 1f, "recruited on the second try");
        }

        [UnityTest]
        public IEnumerator Banter_PlaysOnceAtTheSecurityDoor()
        {
            yield return FindDace();
            _dace.Recruit(_interactor);
            _motor.Teleport(new Vector3(38.7f, 0.05f, 0f));
            yield return Until(() => _ui.BarkVisible, 2f, "Dace remarks on the door");
            StringAssert.Contains("old post", _ui.BarkText);
            Assert.IsTrue(_motor.ControlEnabled, "barks don't stop play");
            Assert.IsTrue(_inventory.HasFlag("banter_security_door"));
            yield return Wait(0.2f);
            yield return Shot("23_bark");
        }

        [UnityTest]
        public IEnumerator Banter_NeedsTheCompanion()
        {
            yield return FindDace();
            _motor.Teleport(new Vector3(38.7f, 0.05f, 0f));
            yield return Wait(0.6f);
            Assert.IsFalse(_ui.BarkVisible);
            Assert.IsFalse(_inventory.HasFlag("banter_security_door"));
        }

        [UnityTest]
        public IEnumerator Follower_CatchesUpToAnotherFloor()
        {
            yield return FindDace();
            _dace.Recruit(_interactor);
            _motor.Teleport(new Vector3(26f, 4.1f, 0f)); // catwalk
            yield return Until(() => _follower.transform.position.y > 3.8f, 3f, "Dace reaches the catwalk");
            Assert.That(Mathf.Abs(_follower.transform.position.x - X), Is.LessThan(2.5f));
        }
    }
}
