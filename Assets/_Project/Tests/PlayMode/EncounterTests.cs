using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Companions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>The optional Security encounter: engage, play turns, win and lose flows.</summary>
    public class EncounterTests : GreyboxTestBase
    {
        private EncounterController _enc;

        private IEnumerator EnterSecurity(bool withDace = true)
        {
            _enc = SecurityEncounter;
            Assert.IsNotNull(_enc, "Encounter_Security missing from the scene (rebuild the greybox level)");
            if (withDace) Object.FindAnyObjectByType<CompanionNPC>().Recruit(_interactor);
            _motor.Teleport(new Vector3(44.5f, 0.05f, 0f));
            yield return Until(() => _interactor.Current == _enc, 2f, "encounter prompt");
        }

        private IEnumerator Settle(string step, float timeout = 10f)
        {
            yield return Until(() => !_enc.IsBusy, timeout, step);
        }

        [UnityTest]
        public IEnumerator Engage_WithKeyboard_PlaysATurn()
        {
            yield return EnterSecurity();
            yield return Tap(Key.E);
            yield return Until(() => _enc.Status == EncounterStatus.Active, 1f, "encounter starts");
            yield return Settle("opening events");

            CombatState st = _enc.State;
            Assert.AreEqual(Phase.Player, st.Phase);
            Assert.AreEqual(2, new List<Unit>(st.Alive(Team.Player)).Count, "Subject + Dace");
            Assert.AreEqual(3, new List<Unit>(st.Alive(Team.Enemy)).Count);
            Assert.IsTrue(_motor.ExternallyControlled, "player body should be under encounter control");
            foreach (Unit e in st.Alive(Team.Enemy)) Assert.IsNotNull(e.Intent, $"{e.Name} has no intent");
            Assert.AreEqual(1, st.Turn, "the E that engaged must not also end the turn");
            yield return Wait(0.3f);
            yield return Shot("10_combat_start");

            // Move the Subject two tiles right with the keyboard.
            Unit subject = _enc.Selected;
            Assert.AreEqual("Subject", subject.Name);
            Tile start = subject.Tile;
            yield return Tap(Key.D);
            yield return Tap(Key.D);
            Assert.AreEqual(start.Col + 2, _enc.Cursor.Col, "cursor follows D presses");
            yield return Tap(Key.Space);
            yield return Settle("subject moves");
            Assert.AreEqual(start.Col + 2, subject.Tile.Col, "Subject moved to the cursor");
            Assert.That(_motor.transform.position.x, Is.EqualTo(subject.Tile.World.x).Within(0.05f), "body follows the unit");

            // Switch to the companion, preview a skill, then end the turn.
            yield return Tap(Key.LeftShift);
            Assert.AreEqual("Dace", _enc.Selected.Name);
            yield return Tap(Key.Digit2);
            Assert.AreEqual(0, _enc.Mode, "2 selects the first skill");
            yield return Wait(0.2f);
            yield return Shot("11_skill_preview");
            yield return Tap(Key.C);
            Assert.AreEqual(-1, _enc.Mode, "C cancels back to move");

            yield return Tap(Key.E);
            yield return Until(() => st.Turn == 2 || st.IsOver, 10f, "enemy phase finishes");
            yield return Settle("enemy phase animation");
            Debug.Log("[RouteTest] log: " + string.Join(" | ", st.Log));
            yield return Wait(0.2f);
            yield return Shot("12_turn2");
        }

        [UnityTest]
        public IEnumerator WithoutCompanion_SubjectFightsAlone()
        {
            yield return EnterSecurity(withDace: false);
            _enc.Engage(_interactor);
            yield return Settle("opening events");
            Assert.AreEqual(1, new List<Unit>(_enc.State.Alive(Team.Player)).Count, "only the Subject");
        }

        [UnityTest]
        public IEnumerator Win_GrantsRewardsAndReturnsControl()
        {
            yield return EnterSecurity();
            int scrapBefore = _inventory.Scrap;
            int xpBefore = _inventory.Xp;
            _enc.Engage(_interactor);
            yield return Settle("opening events");
            CombatState st = _enc.State;
            int hazardTiles = 0;
            foreach (Tile t in st.Grid.Tiles) if (t.Hazard > 0) hazardTiles++;
            Assert.AreEqual(1, hazardTiles, "the arc grate under the platform is live during the fight");

            // Make the fight short and safe; the rules themselves are covered by EditMode tests.
            foreach (Unit u in st.Units)
            {
                if (u.Team == Team.Enemy) u.Hp = 1;
                else if (u.IsLeader) u.Hp = 99;
            }

            for (int turn = 0; turn < 10 && !st.IsOver; turn++)
            {
                foreach (Unit u in new List<Unit>(st.Alive(Team.Player)))
                {
                    if (st.IsOver) break;
                    yield return ActTowardEnemies(st, u);
                }
                if (st.IsOver) break;
                st.EndPlayerTurn();
                yield return Settle($"enemy phase {turn + 1}");
            }

            Assert.AreEqual(Phase.Victory, st.Phase, "expected to win: " + string.Join(" | ", st.Log));
            yield return Wait(0.3f);
            yield return Shot("13_victory");
            yield return Until(() => _enc.Status == EncounterStatus.Cleared, 3f, "encounter wraps up");

            Assert.AreEqual(scrapBefore + 30, _inventory.Scrap, "scrap reward");
            Assert.AreEqual(xpBefore + 50, _inventory.Xp, "XP reward");
            Assert.AreEqual(2, Progression.Level(_inventory.Xp), "first win reaches level 2");
            Assert.IsTrue(_inventory.HasFlag("encounter_security_cleared"));
            Assert.IsFalse(_motor.ExternallyControlled);
            Assert.IsFalse(_enc.IsAvailable(_interactor), "a cleared encounter can't be engaged again");

            float x0 = X;
            Press(Key.D);
            yield return Wait(0.4f);
            Release(Key.D);
            Assert.Greater(X, x0 + 0.5f, "free movement is back");
        }

        [UnityTest]
        public IEnumerator Xp_RaisesMaxHpInTheNextFight()
        {
            yield return EnterSecurity();
            _inventory.AddXp(Progression.XpPerLevel);
            _enc.Engage(_interactor);
            yield return Settle("opening events");
            foreach (Unit u in _enc.State.Alive(Team.Player))
            {
                Assert.AreEqual(u.Def.MaxHp + Progression.HpPerLevel, u.MaxHp, $"{u.Name} gets the level bonus");
                Assert.AreEqual(u.MaxHp, u.Hp);
            }
        }

        [UnityTest]
        public IEnumerator Defeat_ResetsToBeforeEngaging()
        {
            yield return EnterSecurity();
            Vector3 before = _motor.transform.position;
            _enc.Engage(_interactor);
            yield return Settle("opening events");
            CombatState st = _enc.State;
            foreach (Unit u in st.Units)
            {
                if (u.IsLeader) u.Hp = 1;
            }

            for (int turn = 0; turn < 6 && !st.IsOver; turn++)
            {
                st.EndPlayerTurn();
                yield return Settle($"enemy phase {turn + 1}");
            }
            Assert.AreEqual(Phase.Defeat, st.Phase, "expected to lose: " + string.Join(" | ", st.Log));
            yield return Until(() => _enc.Status == EncounterStatus.Idle, 3f, "encounter resets");

            Assert.IsFalse(_motor.ExternallyControlled);
            Assert.That(Vector3.Distance(_motor.transform.position, before), Is.LessThan(0.3f), "player back where they engaged");
            Assert.AreEqual(3, IdleHostilesBetween(42f, 56f), "three idle hostiles respawned");
            yield return Until(() => _interactor.Current == _enc, 2f, "encounter can be engaged again");
        }


        /// <summary>Move to a tile from which the first skill hits an enemy and use it; otherwise close distance.</summary>
        private IEnumerator ActTowardEnemies(CombatState st, Unit u)
        {
            SkillDef s = u.Def.Skills[0];
            Tile attackFrom = null;
            Tile target = null;
            foreach (KeyValuePair<Tile, int> kv in st.MoveOptions(u))
            {
                foreach (Unit e in st.Alive(Team.Enemy))
                {
                    if (st.InRange(kv.Key, s, e.Tile))
                    {
                        attackFrom = kv.Key;
                        target = e.Tile;
                        break;
                    }
                }
                if (attackFrom != null) break;
            }

            if (attackFrom != null)
            {
                if (attackFrom != u.Tile) st.TryMove(u, attackFrom);
                yield return Settle($"{u.Name} moves to attack");
                Assert.IsTrue(st.TryUseSkill(u, 0, target), $"{u.Name} {s.Name} on {target}");
                yield return Settle($"{u.Name} attacks");
                yield break;
            }

            Tile best = null;
            float bestD = float.MaxValue;
            foreach (KeyValuePair<Tile, int> kv in st.MoveOptions(u))
            {
                foreach (Unit e in st.Alive(Team.Enemy))
                {
                    float d = Mathf.Abs(e.Tile.Col - kv.Key.Col) + Mathf.Abs(e.Tile.Y - kv.Key.Y);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = kv.Key;
                    }
                }
            }
            if (best != null && best != u.Tile) st.TryMove(u, best);
            yield return Settle($"{u.Name} advances");
        }
    }
}
