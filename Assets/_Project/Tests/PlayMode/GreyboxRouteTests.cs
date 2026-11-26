using System.Collections;
using Game.Core;
using Game.Player;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Plays the Greybox_Intake route with a virtual keyboard, the way a player would.</summary>
    public class GreyboxRouteTests : GreyboxTestBase
    {
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
    }
}
