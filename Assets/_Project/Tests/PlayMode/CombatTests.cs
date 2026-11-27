using System.Collections;
using Game.Combat;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Real-time combat: the strike, telegraphed enemy attacks, HP and death, rooms, the Warden's armour, bolts, grates.</summary>
    public class CombatTests : GreyboxTestBase
    {
        private PlayerCombat Combat => _motor.GetComponent<PlayerCombat>();

        private static Enemy First(EnemyRoom room, EnemyKind kind)
        {
            foreach (Enemy e in room.Enemies) if (e.Kind == kind) return e;
            return null;
        }

        /// <summary>Security's far Enforcer (x 54.5), clear of the shock grate at x 50.5.</summary>
        private static Enemy FarEnforcer
        {
            get
            {
                Enemy best = null;
                foreach (Enemy e in SecurityRoom.Enemies)
                    if (e.Kind == EnemyKind.Enforcer && (best == null || e.transform.position.x > best.transform.position.x)) best = e;
                return best;
            }
        }

        /// <summary>Takes the room's other hostiles out of play, so one enemy can be tested alone.</summary>
        private static void Only(EnemyRoom room, Enemy keep)
        {
            foreach (Enemy e in room.Enemies) if (e != keep) e.gameObject.SetActive(false);
        }

        private IEnumerator StandAt(float x, float y, int facing)
        {
            _motor.Teleport(new Vector3(x, y + 0.05f, 0f));
            _motor.Face(facing);
            yield return Wait(0.25f);
        }

        private IEnumerator Swing()
        {
            Assert.IsTrue(Combat.TryAttack(), "strike starts");
            yield return Wait(PlayerCombat.Startup + PlayerCombat.Active + PlayerCombat.Recovery + 0.1f);
        }

        [TearDown]
        public void PassiveAgain() => Enemy.Passive = true;

        [UnityTest]
        public IEnumerator Strike_ThreeHitsDropAnEnforcer_AndJAttacks()
        {
            Enemy enf = First(SecurityRoom, EnemyKind.Enforcer);
            yield return StandAt(enf.transform.position.x - 1.0f, 0f, 1);
            int swings = Combat.SwingCount;
            yield return Tap(Key.J);
            Assert.AreEqual(swings + 1, Combat.SwingCount, "J swings");
            yield return Wait(0.4f);
            Assert.AreEqual(2, enf.Body.Hp, "first strike lands");
            yield return Swing();
            yield return Shot("40_strike");
            yield return Swing();
            Assert.IsTrue(enf.Dead, "three strikes drop an Enforcer");
            Assert.AreEqual(Enemy.Phase.Dead, enf.State);
        }

        [UnityTest]
        public IEnumerator Strike_InTheAir_AndCrouchedLow()
        {
            Enemy enf = First(SecurityRoom, EnemyKind.Enforcer);
            yield return StandAt(enf.transform.position.x - 1.0f, 0f, 1);
            Press(Key.C);
            yield return Wait(0.2f);
            Assert.IsTrue(_motor.IsCrouching);
            yield return Swing();
            Release(Key.C);
            Assert.AreEqual(2, enf.Body.Hp, "a low strike hits a standing enemy");
            yield return Wait(0.3f);
            Press(Key.Space);
            yield return Wait(0.06f);
            Assert.AreNotEqual(Game.Player.MotorState.Grounded, _motor.State);
            yield return Swing();
            Release(Key.Space);
            Assert.AreEqual(1, enf.Body.Hp, "a jumping strike hits");
        }

        [UnityTest]
        public IEnumerator Enforcer_TelegraphsItsAttack_ThenTheHitKnocksYouBack()
        {
            Enemy.Passive = false;
            Enemy enf = FarEnforcer;
            Only(SecurityRoom, enf);
            yield return StandAt(enf.transform.position.x - 2.5f, 0f, 1);
            Transform alert = enf.transform.Find("Alert");
            yield return Until(() => enf.State == Enemy.Phase.Windup, 3f, "wind-up");
            Assert.IsTrue(alert.gameObject.activeSelf, "the ! shows during the wind-up");
            Assert.AreEqual(5, Combat.Hp, "no damage during the wind-up");
            yield return Shot("41_windup");
            yield return Until(() => Combat.Hp < 5, 2f, "the lunge lands");
            Assert.AreEqual(4, Combat.Hp);
            Assert.IsTrue(Combat.Body.Invulnerable, "a moment of invulnerability");
            Assert.IsTrue(_motor.Stunned, "knocked back");
            Assert.Less(_motor.Velocity.x, 0f, "thrown away from the Enforcer");
        }

        [UnityTest]
        public IEnumerator Strike_InterruptsTheWindup()
        {
            Enemy.Passive = false;
            Enemy enf = FarEnforcer;
            Only(SecurityRoom, enf);
            yield return StandAt(enf.transform.position.x - 1.2f, 0f, 1);
            yield return Until(() => enf.State == Enemy.Phase.Windup, 3f, "wind-up");
            Assert.IsTrue(Combat.TryAttack());
            yield return Until(() => enf.State == Enemy.Phase.Flinch, 1f, "flinch");
            Assert.AreEqual(5, Combat.Hp, "struck first, took nothing");
        }

        [UnityTest]
        public IEnumerator Dying_SendsYouToTheCheckpoint_AndResetsTheRoom()
        {
            Enemy.Passive = false;
            EnemyRoom room = SecurityRoom;
            _respawn.SetCheckpoint(new Vector3(41.5f, 0.05f, 0f));
            Enemy enf = FarEnforcer;
            Only(room, enf);
            yield return StandAt(enf.transform.position.x - 1.0f, 0f, 1);
            enf.Body.Damage(new HitInfo { Damage = 1, From = _motor.transform.position, Source = Combat.Body });
            Assert.AreEqual(2, enf.Body.Hp);
            Combat.Body.Damage(new HitInfo { Damage = 99, From = enf.transform.position });
            yield return Until(() => _motor.transform.position.x < 42.5f, 3f, "back at the checkpoint");
            yield return Wait(0.2f);
            Assert.AreEqual(Combat.MaxHp, Combat.Hp, "HP refilled");
            Assert.AreEqual(enf.Body.MaxHp, enf.Body.Hp, "the room reset");
            Assert.That(enf.transform.position.x, Is.EqualTo(enf.Spawn.x).Within(0.01f));
            Assert.IsFalse(room.Cleared);
        }

        [UnityTest]
        public IEnumerator ClearingARoom_PaysOnce_AndRaisesMaxHp()
        {
            EnemyRoom room = SecurityRoom;
            yield return StandAt(44.5f, 0f, 1);
            int scrap = _inventory.Scrap;
            foreach (Enemy e in room.Enemies) e.Body.Damage(new HitInfo { Damage = 99, From = e.transform.position, Source = Combat.Body });
            yield return Until(() => room.Cleared, 2f, "cleared");
            Assert.IsTrue(_inventory.HasFlag("encounter_security_cleared"));
            Assert.AreEqual(scrap + 30, _inventory.Scrap);
            Assert.AreEqual(50, _inventory.Xp);
            yield return null;
            Assert.AreEqual(PlayerCombat.BaseHp + 1, Combat.MaxHp, "level 2: one more HP");
            yield return Wait(0.5f);
            Assert.AreEqual(scrap + 30, _inventory.Scrap, "paid once");
        }

        [UnityTest]
        public IEnumerator Warden_ArmourBlocksFromTheFront_ButNotFromBehind()
        {
            Enemy warden = First(ServersRoom, EnemyKind.Warden);
            Assert.AreEqual(-1, warden.Facing, "faces the way you come in");
            yield return StandAt(warden.transform.position.x - 1.3f, 0f, 1);
            yield return Swing();
            Assert.AreEqual(HitResult.Blocked, Combat.LastResult, "blocked from the front");
            Assert.AreEqual(8, warden.Body.Hp);
            yield return Wait(0.5f);
            yield return StandAt(warden.transform.position.x + 1.3f, 0f, -1);
            yield return Swing();
            Assert.AreEqual(HitResult.Hit, Combat.LastResult, "hit from behind");
            Assert.AreEqual(7, warden.Body.Hp);
        }

        [UnityTest]
        public IEnumerator ShockGrate_HurtsHostilesToo()
        {
            Enemy.Passive = false;
            EnemyRoom room = ServersRoom;
            Enemy warden = First(room, EnemyKind.Warden);
            yield return StandAt(75f, 0f, 1);
            Assert.IsTrue(room.PlayerInside);
            warden.transform.position = new Vector3(81.5f, 0.02f, 0f);
            yield return Until(() => warden.Body.Hp < 8, 1.5f, "the grate shocks the Warden");
        }

        [UnityTest]
        public IEnumerator Bolt_IsKnockedOutOfTheAir()
        {
            yield return StandAt(44f, 0f, 1);
            Bolt.Fire(new Vector3(45.2f, 1.2f, 0f), new Vector3(-1f, 0f, 0f), 1);
            Assert.IsTrue(Combat.TryAttack());
            yield return Wait(0.25f);
            Assert.AreEqual(0, Bolt.Live.Count, "struck down");
            Assert.AreEqual(5, Combat.Hp);
        }

        [UnityTest]
        public IEnumerator Touch_TapsDontSwing_TheAttackButtonDoes()
        {
            TouchControls.ForceVisible = true;
            try
            {
                yield return StandAt(44f, 0f, 1);
                int swings = Combat.SwingCount;
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                try
                {
                    InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
                    yield return null;
                }
                finally
                {
                    InputSystem.RemoveDevice(mouse);
                }
                Assert.AreEqual(swings, Combat.SwingCount, "a tap (mouse click) doesn't swing on touch");
                Gamepad pad = Gamepad.current;
                Assert.IsNotNull(pad, "the on-screen controls' virtual gamepad");
                InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.West));
                yield return null;
                InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState());
                yield return null;
                Assert.AreEqual(swings + 1, Combat.SwingCount, "ATTACK swings");
            }
            finally
            {
                TouchControls.ForceVisible = false;
            }
        }
    }
}
