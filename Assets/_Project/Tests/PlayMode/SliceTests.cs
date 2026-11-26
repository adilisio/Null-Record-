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
        public IEnumerator ServerHall_Encounter_HasThreeEnemyTypes_AndTwoHazards()
        {
            EncounterController enc = GameObject.Find("Encounter_Servers")?.GetComponent<EncounterController>();
            Assert.IsNotNull(enc, "Encounter_Servers missing (rebuild scenes)");
            Assert.AreEqual(3, IdleHostilesBetween(74f, 90f), "three idle hostiles in the Server Hall");

            _motor.Teleport(new Vector3(75.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == enc, 2f, "encounter prompt");
            enc.Engage(_interactor);
            yield return Until(() => !enc.IsBusy, 5f, "opening events");

            var names = new List<string>();
            foreach (Unit u in enc.State.Alive(Team.Enemy)) names.Add(u.Name);
            CollectionAssert.AreEquivalent(new[] { "Enforcer", "Sentry", "Warden" }, names);
            int hazards = 0;
            foreach (Tile t in enc.State.Grid.Tiles) if (t.Hazard > 0) hazards++;
            Assert.AreEqual(2, hazards, "two arc grates");
            foreach (Unit e in enc.State.Alive(Team.Enemy)) Assert.IsNotNull(e.Intent, $"{e.Name} plans");
            yield return Wait(1.2f); // camera pulls back to frame the fight
            foreach (Unit u in enc.State.Units)
            {
                if (!u.Alive) continue;
                Vector3 vp = Camera.main.WorldToViewportPoint(u.Tile.World + Vector3.up);
                Assert.That(vp.x, Is.InRange(0.02f, 0.98f), $"{u.Name} is on screen (x)");
                Assert.That(vp.y, Is.InRange(0.02f, 0.98f), $"{u.Name} is on screen (y)");
            }
            yield return Shot("16_servers_combat");
        }
    }
}
