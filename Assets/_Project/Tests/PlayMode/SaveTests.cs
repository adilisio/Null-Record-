using System.Collections;
using Game.Combat;
using Game.Companions;
using Game.Core;
using Game.Save;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Autosave triggers and restoring a session by reloading the scene.</summary>
    public class SaveTests : GreyboxTestBase
    {
        private static SaveManager Saves => SaveManager.Instance;

        [UnityTest]
        public IEnumerator FreshStart_HasNoSaveAndDoesNotLoad()
        {
            Assert.IsNotNull(Saves, "SaveManager missing from the scene (rebuild the greybox level)");
            Assert.IsFalse(Saves.LoadedFromSave);
            yield return Wait(0.5f);
            Assert.IsFalse(SaveSystem.Exists, "standing at the start shouldn't save");
        }

        [UnityTest]
        public IEnumerator EnteringARoom_Autosaves_AndMovesRespawn()
        {
            _motor.Teleport(new Vector3(24f, 0.05f, 0f)); // Maintenance floor
            yield return Until(() => SaveSystem.Exists, 3f, "room-entry autosave");
            Assert.AreEqual("room", Saves.LastSaveReason);
            Assert.AreEqual("Zone_B", Saves.CurrentRoom);
            Assert.That(_respawn.Checkpoint.x, Is.EqualTo(24f).Within(0.2f), "respawn moves to the room entry");
            Assert.AreEqual("Zone_B", SaveSystem.Read().room);
        }

        [UnityTest]
        public IEnumerator PickupAndPosition_SurviveAReload()
        {
            _motor.Teleport(new Vector3(27.2f, 4.1f, 0f)); // catwalk, by the Thermal Lining
            yield return Until(() => _interactor.Current is ItemPickup, 2f, "lining prompt");
            yield return Tap(Key.E);
            yield return Until(() => _inventory.IsProtectedFrom(HazardType.Heat), 1f, "take the lining");
            int before = Saves.SaveCount;
            yield return Until(() => Saves.SaveCount > before, 3f, "autosave after pickup");
            yield return Wait(0.1f);
            yield return Shot("30_saving");

            yield return LoadScene();
            Assert.IsTrue(Saves.LoadedFromSave);
            Assert.IsTrue(_inventory.IsProtectedFrom(HazardType.Heat), "inventory restored");
            Assert.IsTrue(_inventory.HasFlag("taken_pickup_thermal_lining"));
            Assert.That(X, Is.EqualTo(27.2f).Within(0.4f), "position restored");
            Assert.That(Y, Is.EqualTo(4f).Within(0.3f), "back on the catwalk");
            foreach (ItemPickup p in Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
            {
                Assert.AreNotEqual("pickup_thermal_lining", p.SaveId, "a taken pickup stays taken");
            }
        }

        [UnityTest]
        public IEnumerator DoorAndCompanion_SurviveAReload()
        {
            DaceNpc.Recruit(_interactor);
            _inventory.GrantKeycard(2);
            _motor.Teleport(new Vector3(38.9f, 0.05f, 0f));
            yield return Until(() => _interactor.Current is Door, 2f, "door prompt");
            yield return Tap(Key.E);
            int before = Saves.SaveCount;
            yield return Until(() => Saves.SaveCount > before, 3f, "autosave after opening");
            Assert.AreEqual("Guard", SaveSystem.Read().companion);

            yield return LoadScene();
            Door door = GameObject.Find("Door_L2").GetComponent<Door>();
            Assert.IsTrue(door.IsOpen, "door stays open");
            Assert.AreEqual(2, _inventory.KeycardLevel);
            CompanionFollower dace = CompanionFollower.Active;
            Assert.IsNotNull(dace, "Dace is back in the party");
            Assert.IsTrue(DaceNpc.Recruited);
            Assert.That(Mathf.Abs(dace.transform.position.x - X), Is.LessThan(2.5f), "Dace starts beside the player");

            Press(Key.D);
            yield return Until(() => X > 41f, 3f, "walk through the open door");
            Release(Key.D);
        }

        [UnityTest]
        public IEnumerator ClearedRoom_StaysClearedAfterReload()
        {
            EnemyRoom room = SecurityRoom;
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Wait(0.2f);
            Combatant me = _motor.GetComponent<Combatant>();
            foreach (Enemy e in room.Enemies) e.Body.Damage(new HitInfo { Damage = 99, From = e.transform.position, Source = me });
            yield return Until(() => room.Cleared, 2f, "room cleared");
            yield return Until(() => SavedFlag("encounter_security_cleared"), 3f, "autosave after the win");

            yield return LoadScene();
            room = SecurityRoom;
            yield return null;
            Assert.IsTrue(room.Cleared, "a cleared room stays cleared");
            Assert.AreEqual(30, _inventory.Scrap);
            Assert.AreEqual(0, IdleHostilesBetween(42f, 56f), "no hostiles come back in Security");
        }

        private static bool SavedFlag(string flag)
        {
            SaveData d = SaveSystem.Read();
            return d != null && System.Array.IndexOf(d.flags, flag) >= 0;
        }


        [UnityTest]
        public IEnumerator CorruptSave_StartsFresh()
        {
            System.IO.Directory.CreateDirectory(SaveSystem.Folder);
            System.IO.File.WriteAllText(SaveSystem.FilePath, "not json");
            LogAssert.ignoreFailingMessages = true;
            yield return LoadScene();
            Assert.IsFalse(Saves.LoadedFromSave);
            Assert.That(X, Is.EqualTo(1f).Within(0.3f), "fresh start position");
        }
    }
}
