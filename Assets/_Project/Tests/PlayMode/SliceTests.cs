using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Player;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>The expansion east of Security: Records (ladder, loft, spoofer), the laser grid, and the Server Hall fight.</summary>
    public class SliceTests : GreyboxTestBase
    {
        [UnityTest]
        public IEnumerator Records_LadderToLoft_PickUpSpoofer_ThenSpoofTheGrid()
        {
            LaserGrid grid = GameObject.Find("LaserGrid_Servers")?.GetComponent<LaserGrid>();
            Assert.IsNotNull(grid, "LaserGrid_Servers missing (rebuild scenes)");

            // The grid blocks without the spoofer.
            _motor.Teleport(new Vector3(70.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == grid, 2f, "grid prompt");
            StringAssert.Contains("needed", grid.GetPrompt(_interactor));
            yield return Tap(Key.E);
            Assert.IsFalse(grid.IsOpen, "a live grid stays shut without the tool");
            Press(Key.D);
            yield return Wait(1f);
            Release(Key.D);
            Assert.Less(X, 72f, "the grid is a solid barrier");
            yield return Shot("15_laser_grid");

            // Up the ladder to the loft.
            _motor.Teleport(new Vector3(62.2f, 0.05f, 0f));
            yield return Until(() => _motor.State == MotorState.Grounded, 1f, "land by the ladder");
            Press(Key.D);
            yield return Until(() => X >= 62.75f, 2f, "walk to the ladder");
            Release(Key.D);
            yield return Until(() => _motor.State == MotorState.Grounded && Mathf.Abs(_motor.Velocity.x) < 0.05f, 1f, "stop at ladder");
            Press(Key.W);
            yield return Until(() => _motor.State == MotorState.Ladder, 1f, "grab ladder");
            yield return Until(() => _motor.State == MotorState.Grounded && Y > 3.9f, 4f, "exit ladder onto the loft");
            Release(Key.W);

            Press(Key.D);
            yield return Until(() => X >= 68.2f, 4f, "walk along the loft");
            Release(Key.D);
            yield return Until(() => _interactor.Current is ItemPickup, 1f, "spoofer prompt");
            yield return Shot("14_records_spoofer");
            yield return Tap(Key.E);
            yield return Until(() => _inventory.HasTool(LaserGrid.SpooferTool), 1f, "receive the spoofer");

            // Back down and through the grid.
            _motor.Teleport(new Vector3(70.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == grid, 2f, "grid prompt again");
            StringAssert.Contains("Spoof", grid.GetPrompt(_interactor));
            yield return Tap(Key.E);
            Assert.IsTrue(grid.IsOpen, "spoofed");
            Assert.IsTrue(_inventory.HasFlag("opened_grid_servers"), "remembered for saves");
            Press(Key.D);
            yield return Until(() => X > 73.5f, 3f, "walk into the Server Hall");
            Release(Key.D);
        }

        [UnityTest]
        public IEnumerator ServerHall_HasThreeKindsOfHostile_AndTwoShockGrates()
        {
            EnemyRoom room = ServersRoom;
            Assert.AreEqual(3, IdleHostilesBetween(74f, 90f), "three hostiles in the Server Hall");
            var kinds = new List<string>();
            foreach (Enemy e in room.Enemies) kinds.Add(e.Kind.ToString());
            CollectionAssert.AreEquivalent(new[] { "Enforcer", "Sentry", "Warden" }, kinds);
            int grates = 0;
            foreach (ShockGrate g in Object.FindObjectsByType<ShockGrate>(FindObjectsSortMode.None))
                if (g.transform.position.x > 74f && g.transform.position.x < 90f) grates++;
            Assert.AreEqual(2, grates, "two arc grates");
            yield return null;
        }
    }
}
