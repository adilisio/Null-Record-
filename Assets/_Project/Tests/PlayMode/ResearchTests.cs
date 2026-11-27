using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Companions;
using Game.Player;
using Game.Save;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Region 2, the Research Wing: the lift, the remote override and its bridge, companion 2 and the swap terminal.</summary>
    public class ResearchTests : GreyboxTestBase
    {
        private const float R2Y = -30f;

        private static T Named<T>(string name) where T : Component
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, $"{name} missing (rebuild scenes)");
            return go.GetComponent<T>();
        }

        private static CompanionNPC Researcher => Named<CompanionNPC>("Researcher");

        [UnityTest]
        public IEnumerator FreightLift_CarriesYouToTheResearchWing_AndBack()
        {
            FreightLift down = Named<FreightLift>("FreightLift_Down");
            _motor.Teleport(new Vector3(88.6f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == down, 2f, "lift prompt");
            yield return Tap(Key.E);
            yield return Wait(0.15f);
            Assert.Greater(Game.UI.ScreenFade.Alpha, 0.2f, "the lift ride fades in from black");
            yield return Wait(0.15f);
            Assert.Less(Y, R2Y + 1f, "arrived in the Research Wing");
            Assert.That(X, Is.EqualTo(102.5f).Within(0.5f));
            Assert.Less(Camera.main.transform.position.y, R2Y + 10f, "the camera came along");
            yield return Wait(0.5f);
            yield return Shot("21_research_lobby");

            FreightLift up = Named<FreightLift>("FreightLift_Up");
            _motor.Teleport(new Vector3(101.2f, R2Y + 0.05f, 0f));
            yield return Until(() => _interactor.Current == up, 2f, "lift prompt below");
            yield return Tap(Key.E);
            yield return Wait(0.3f);
            Assert.Greater(Y, -1f, "back up in the Server Hall");
        }

        [UnityTest]
        public IEnumerator Pit_CantBeJumpedOrClimbed_ButTheOverrideExtendsTheBridge()
        {
            RemoteConsole console = Named<RemoteConsole>("Console_Bridge");

            // A full sprint jump from the edge falls short.
            _motor.Teleport(new Vector3(117.5f, R2Y + 0.05f, 0f));
            yield return Until(() => _motor.State == MotorState.Grounded, 1f, "land in the Labs");
            Press(Key.LeftShift);
            Press(Key.D);
            yield return Until(() => X >= 121.55f, 3f, "sprint to the pit edge");
            yield return Tap(Key.Space);
            yield return Wait(1.5f);
            Release(Key.D);
            Release(Key.LeftShift);
            Assert.Less(Y, R2Y - 2f, "a sprint jump can't clear the 7 m gap");

            // The far wall is too tall to ledge-climb out of.
            _motor.Teleport(new Vector3(128.4f, R2Y - 4.45f, 0f));
            yield return Until(() => _motor.State == MotorState.Grounded, 1f, "land in the pit");
            Press(Key.D);
            for (int i = 0; i < 3; i++)
            {
                yield return Tap(Key.Space);
                yield return Wait(0.7f);
            }
            Release(Key.D);
            Assert.Less(Y, R2Y - 2f, "no way out on the far side");

            // The ladder leads back to the near side.
            Press(Key.A);
            yield return Until(() => X <= 122.8f, 4f, "walk to the pit ladder");
            Release(Key.A);
            yield return Until(() => _motor.State == MotorState.Grounded && Mathf.Abs(_motor.Velocity.x) < 0.05f, 1f, "stop at the ladder");
            Press(Key.W);
            yield return Until(() => _motor.State == MotorState.Ladder, 1f, "grab the pit ladder");
            yield return Until(() => _motor.State == MotorState.Grounded && Y > R2Y - 0.5f, 5f, "climb out onto the near side");
            Release(Key.W);

            // Without the tool the console is out of reach from this side.
            _motor.Teleport(new Vector3(120.8f, R2Y + 0.05f, 0f));
            yield return Wait(0.3f);
            Assert.AreNotEqual(console, _interactor.Current, "no override yet");

            // Take the override from the lab bench.
            _motor.Teleport(new Vector3(115.5f, R2Y + 1.3f, 0f));
            yield return Until(() => _interactor.Current is ItemPickup, 2f, "override prompt");
            yield return Shot("22_override_bench");
            yield return Tap(Key.E);
            yield return Until(() => _inventory.HasTool(RemoteConsole.OverrideTool), 1f, "receive the remote override");

            // Operate the bridge from across the gap, then walk over.
            _motor.Teleport(new Vector3(120.8f, R2Y + 0.05f, 0f));
            yield return Until(() => _interactor.Current == console, 2f, "remote console prompt from across the gap");
            StringAssert.Contains("Remote override", console.GetPrompt(_interactor));
            yield return Tap(Key.E);
            Assert.IsTrue(console.IsOpen);
            Assert.IsTrue(_inventory.HasFlag("opened_bridge_labs"), "remembered for saves");
            yield return Wait(1.5f);
            yield return Shot("23_bridge");
            Press(Key.D);
            yield return Until(() => X > 130f, 4f, "walk across the bridge");
            Release(Key.D);
            Assert.Greater(Y, R2Y - 0.5f, "stayed on top");
        }

        [UnityTest]
        public IEnumerator Researcher_Joins_DaceWaits_ThenTheTerminalSwapsThem()
        {
            DaceNpc.Recruit(_interactor);
            CompanionFollower dace = DaceNpc.GetComponent<CompanionFollower>();
            CompanionFollower doc = Researcher.GetComponent<CompanionFollower>();
            Assert.AreEqual("Researcher", doc.CombatUnit);

            _motor.Teleport(new Vector3(148.8f, R2Y + 0.05f, 0f));
            yield return Until(() => _interactor.Current == Researcher, 2f, "talk prompt");
            Researcher.Recruit(_interactor);
            yield return null;
            Assert.AreEqual(doc, CompanionFollower.Active, "the researcher follows now");
            Assert.IsFalse(dace.enabled, "Dace stopped following");
            Assert.That(dace.transform.position.x, Is.InRange(106f, 111f), "Dace waits at the terminal");
            Assert.Less(dace.transform.position.y, R2Y + 1f);
            Assert.IsTrue(_inventory.HasFlag("companion_researcher"));

            CompanionTerminal terminal = Named<CompanionTerminal>("CompanionTerminal");
            _motor.Teleport(new Vector3(106.2f, R2Y + 0.05f, 0f));
            yield return Until(() => _interactor.Current == terminal, 2f, "terminal prompt");
            StringAssert.Contains("Researcher", terminal.GetPrompt(_interactor));
            yield return Tap(Key.E);
            Assert.AreEqual(dace, CompanionFollower.Active, "swapped back to Dace");
            Assert.IsFalse(doc.enabled, "the researcher waits");
            Assert.That(doc.transform.position.x, Is.InRange(106f, 111f));
            yield return Wait(1.5f); // let the camera catch up with the teleport
            yield return Shot("24_terminal");
        }

        [UnityTest]
        public IEnumerator Save_RestoresWhoFollows_AndWhoWaits()
        {
            DaceNpc.Recruit(_interactor);
            Researcher.Recruit(_interactor); // researcher active, Dace benched
            _motor.Teleport(new Vector3(103.5f, R2Y + 0.05f, 0f));
            yield return Until(() => _motor.State == MotorState.Grounded, 1f, "land in the lobby");
            int before = SaveManager.Instance.SaveCount;
            SaveManager.Instance.SaveNow("test");
            yield return Until(() => SaveManager.Instance.SaveCount > before, 3f, "saved");

            yield return LoadScene();
            CompanionFollower dace = DaceNpc.GetComponent<CompanionFollower>();
            CompanionFollower doc = Researcher.GetComponent<CompanionFollower>();
            Assert.AreEqual(doc, CompanionFollower.Active, "the researcher still follows");
            int following = 0;
            foreach (CompanionFollower f in Object.FindObjectsByType<CompanionFollower>(FindObjectsSortMode.None))
            {
                if (f.enabled && f.IsFollowing) following++;
            }
            Assert.AreEqual(1, following, "exactly one companion follows");
            Assert.IsTrue(DaceNpc.Recruited, "Dace is still recruited");
            Assert.IsFalse(dace.enabled);
            Assert.That(dace.transform.position.x, Is.InRange(106f, 111f), "and waits at the terminal");
        }

        [UnityTest]
        public IEnumerator SpecimenHall_HasThreeKindsOfHostile()
        {
            EnemyRoom room = SpecimensRoom;
            Assert.AreEqual(3, IdleHostilesBetween(134f, 149f), "three hostiles in the Specimen Hall");
            var kinds = new List<string>();
            foreach (Enemy e in room.Enemies) kinds.Add(e.Kind.ToString());
            CollectionAssert.AreEquivalent(new[] { "Enforcer", "Sentry", "Warden" }, kinds);
            _motor.Teleport(new Vector3(135.5f, R2Y + 0.05f, 0f));
            yield return Wait(0.5f);
            Assert.IsTrue(room.PlayerInside);
            yield return Shot("25_specimens_room");
        }

        [UnityTest]
        public IEnumerator FreightShutter_OpensWithTheOverride()
        {
            RemoteConsole shutter = Named<RemoteConsole>("Console_Shutter");
            _inventory.GrantTool(RemoteConsole.OverrideTool);
            _motor.Teleport(new Vector3(151.6f, R2Y + 0.05f, 0f));
            yield return Until(() => _interactor.Current == shutter, 2f, "shutter prompt");
            yield return Tap(Key.E);
            Assert.IsTrue(shutter.IsOpen);
            yield return Wait(1.5f);
            Press(Key.D);
            yield return Until(() => X > 153.5f, 3f, "walk through the shutter");
            Release(Key.D);
        }
    }
}
