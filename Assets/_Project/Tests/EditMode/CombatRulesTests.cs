using System.Collections.Generic;
using Game.Combat;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Grid-combat rules on hand-built grids. No scene, no physics.</summary>
    public class CombatRulesTests
    {
        private const float PlatformY = 2.2f;

        /// <summary>Floor tiles in columns 0..9 at y 0, plus a platform over columns 6..7.</summary>
        private static CombatGrid MakeGrid()
        {
            var g = new CombatGrid();
            for (int c = 0; c < 10; c++) g.AddTile(c, 0f);
            g.AddTile(6, PlatformY);
            g.AddTile(7, PlatformY);
            g.LinkNeighbours();
            return g;
        }

        private static Tile Floor(CombatGrid g, int col) => g.Find(col, 0f);
        private static Tile High(CombatGrid g, int col) => g.Find(col, PlatformY);

        private static CombatState Battle(CombatGrid g) => new CombatState(g);

        // ---------------------------------------------------------------- movement

        [Test]
        public void Reachable_CountsClimbAsTwo()
        {
            CombatGrid g = MakeGrid();
            Dictionary<Tile, int> r = g.Reachable(Floor(g, 5), 2, null);
            Assert.IsTrue(r.ContainsKey(High(g, 6)), "climb onto the platform costs 2");
            Assert.AreEqual(2, r[High(g, 6)]);
            Assert.IsFalse(r.ContainsKey(High(g, 7)), "climb + walk = 3, over budget");
            Assert.IsTrue(r.ContainsKey(Floor(g, 7)), "walking under the platform");
        }

        [Test]
        public void Units_BlockMovement()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 2));
            s.Start();
            Assert.IsFalse(s.TryMove(me, Floor(g, 3)), "can't walk through an enemy");
            Assert.IsTrue(s.TryMove(me, Floor(g, 0)));
            Assert.IsFalse(s.TryMove(me, Floor(g, 1)), "one move per turn");
        }

        // ---------------------------------------------------------------- targeting and damage

        [Test]
        public void Melee_NeedsAdjacentColumnAndSmallHeightGap()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            SkillDef strike = CombatPresets.Strike;
            Assert.IsTrue(s.InRange(Floor(g, 5), strike, Floor(g, 6)));
            Assert.IsFalse(s.InRange(Floor(g, 5), strike, Floor(g, 7)), "two columns away");
            Assert.IsFalse(s.InRange(Floor(g, 5), strike, High(g, 6)), "platform is 2.2 up");
        }

        [Test]
        public void Ranged_RespectsLineOfSightAndCover()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            SkillDef shot = CombatPresets.TaserShot;
            Unit victim = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 4));

            Assert.IsFalse(s.InRange(Floor(g, 3), shot, Floor(g, 4)), "too close for the minimum range");
            Assert.IsTrue(s.InRange(Floor(g, 1), shot, Floor(g, 4)));
            Assert.AreEqual(2, s.PreviewDamage(Floor(g, 1), shot, Floor(g, 4), victim));

            g.Sight = (a, b) => LineOfSight.HalfCover;
            Assert.AreEqual(1, s.PreviewDamage(Floor(g, 1), shot, Floor(g, 4), victim), "half cover halves damage");

            g.Sight = (a, b) => LineOfSight.Blocked;
            Assert.IsFalse(s.InRange(Floor(g, 1), shot, Floor(g, 4)), "no line of sight");
        }

        [Test]
        public void Height_AddsOneDamage_ArmorSubtracts()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit sentryTarget = s.AddUnit(CombatPresets.Sentry, Team.Enemy, Floor(g, 9));
            Unit guardTarget = s.AddUnit(CombatPresets.Guard, Team.Player, Floor(g, 4));
            Assert.AreEqual(3, s.PreviewDamage(High(g, 7), CombatPresets.TaserShot, Floor(g, 9), sentryTarget), "2 + height");
            Assert.AreEqual(1, s.PreviewDamage(Floor(g, 1), CombatPresets.TaserShot, Floor(g, 4), guardTarget), "2 - armor 1");
            Assert.AreEqual(1, s.PreviewDamage(Floor(g, 3), CombatPresets.Shove, Floor(g, 4), guardTarget), "never below 1");
        }

        // ---------------------------------------------------------------- knockback

        [Test]
        public void Shove_IntoWall_DealsImpactDamage()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 0));
            s.Start();
            Assert.IsTrue(s.TryUseSkill(me, 1, Floor(g, 0)));
            Assert.AreEqual(6 - 1 - CombatRules.ImpactDamage, foe.Hp);
            Assert.AreEqual(Floor(g, 0), foe.Tile);
        }

        [Test]
        public void Shove_OffPlatform_DealsFallDamage()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, High(g, 6), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Enforcer, Team.Enemy, High(g, 7));
            s.Start();
            Assert.IsTrue(s.TryUseSkill(me, 1, High(g, 7)));
            Assert.AreEqual(Floor(g, 8), foe.Tile, "pushed off the edge onto the floor, and the push stops there");
            Assert.AreEqual(6 - 1 - CombatRules.FallDamage, foe.Hp);
        }

        [Test]
        public void Shove_IntoAnotherUnit_HurtsBoth()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 2), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 3));
            Unit other = s.AddUnit(CombatPresets.Sentry, Team.Enemy, Floor(g, 4));
            s.Start();
            Assert.IsTrue(s.TryUseSkill(me, 1, Floor(g, 3)));
            Assert.AreEqual(6 - 1 - 1, foe.Hp);
            Assert.AreEqual(4 - 1, other.Hp);
            Assert.AreEqual(Floor(g, 3), foe.Tile);
        }

        // ---------------------------------------------------------------- turns, intents, outcomes

        [Test]
        public void EnemyIntent_TargetsATile_SoMovingAwayDodges()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 3), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 6));
            s.Start();

            Assert.IsNotNull(foe.Intent?.Skill, "the Enforcer plans an attack");
            Assert.AreEqual(Floor(g, 3), foe.Intent.Target, "aimed at where the Subject stands");
            Assert.AreEqual(Floor(g, 4), foe.Intent.MoveTo);

            Assert.IsTrue(s.TryMove(me, Floor(g, 0)));
            s.EndPlayerTurn();
            Assert.AreEqual(8, me.Hp, "attack landed on an empty tile");
            Assert.AreEqual(Floor(g, 4), foe.Tile);
            Assert.AreEqual(Phase.Player, s.Phase);
            Assert.AreEqual(2, s.Turn);
        }

        [Test]
        public void EnemyIntent_LandsIfYouStay()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 3), leader: true);
            s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 6));
            s.Start();
            s.EndPlayerTurn();
            Assert.AreEqual(8 - 3, me.Hp);
        }

        [Test]
        public void Victory_WhenAllEnemiesDown()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Sentry, Team.Enemy, Floor(g, 2));
            foe.Hp = 2;
            s.Start();
            Assert.IsTrue(s.TryUseSkill(me, 0, Floor(g, 2)));
            Assert.AreEqual(Phase.Victory, s.Phase);
            Assert.IsNull(foe.Tile, "dead units leave the grid");
        }

        [Test]
        public void Defeat_OnlyWhenTheLeaderFalls()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 0), leader: true);
            Unit ally = s.AddUnit(CombatPresets.Guard, Team.Player, Floor(g, 5));
            ally.Hp = 1;
            s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 7));
            s.Start();
            s.EndPlayerTurn();
            Assert.IsFalse(ally.Alive, "the companion goes down");
            Assert.AreEqual(Phase.Player, s.Phase, "but the fight goes on");

            me.Hp = 1;
            s.PlanIntents();
            for (int i = 0; i < 4 && !s.IsOver; i++) s.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, s.Phase);
        }

        [Test]
        public void Cooldown_BlocksReuseUntilItTicksDown()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Guard, Team.Enemy, Floor(g, 2)); // tough, no ranged
            s.Start();
            Assert.IsTrue(s.TryUseSkill(me, 1, Floor(g, 2)), "shove");
            Assert.IsFalse(me.SkillReady(1));

            s.EndPlayerTurn();
            Assert.IsFalse(me.SkillReady(1), "cooldown 2 -> 1 after one round");
            s.EndPlayerTurn();
            Assert.IsTrue(me.SkillReady(1), "ready again after two rounds");
            Assert.IsTrue(foe.Alive);
        }

        [Test]
        public void Determinism_SameSetupSameResult()
        {
            string Run()
            {
                CombatGrid g = MakeGrid();
                CombatState s = Battle(g);
                s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 2), leader: true);
                s.AddUnit(CombatPresets.Guard, Team.Player, Floor(g, 1));
                s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 8));
                s.AddUnit(CombatPresets.Sentry, Team.Enemy, High(g, 7));
                s.Start();
                for (int i = 0; i < 3 && !s.IsOver; i++) s.EndPlayerTurn();
                return string.Join("|", s.Log);
            }
            Assert.AreEqual(Run(), Run());
        }

        // ---------------------------------------------------------------- positioning and hazards

        [Test]
        public void RangedEnemy_HoldsHighGround_WhenNoTargetInRange()
        {
            // Wider room: even dropping down, the Sentry can't get within range this turn.
            var g = new CombatGrid();
            for (int c = 0; c < 15; c++) g.AddTile(c, 0f);
            g.AddTile(11, PlatformY);
            g.AddTile(12, PlatformY);
            g.LinkNeighbours();
            CombatState s = Battle(g);
            s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 0), leader: true);
            Unit sentry = s.AddUnit(CombatPresets.Sentry, Team.Enemy, High(g, 12));
            s.Start();
            Assert.IsNull(sentry.Intent.Skill, "nobody in range yet");
            Assert.That(sentry.Intent.MoveTo.Y, Is.EqualTo(PlatformY).Within(0.01f), "stays on the platform instead of dropping to close in");
        }

        [Test]
        public void Hazard_HurtsUnitEndingMoveThere_ButNotPassingThrough()
        {
            CombatGrid g = MakeGrid();
            Floor(g, 3).Hazard = CombatRules.HazardDamage;
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            Unit dace = s.AddUnit(CombatPresets.Guard, Team.Player, Floor(g, 0));
            s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 9));
            s.Start();

            Assert.IsTrue(s.TryMove(me, Floor(g, 4)), "walk over the hazard");
            Assert.AreEqual(me.MaxHp, me.Hp, "passing through is safe");
            Assert.IsTrue(s.TryMove(dace, Floor(g, 3)), "stop on the hazard");
            Assert.AreEqual(dace.MaxHp - CombatRules.HazardDamage, dace.Hp, "armour doesn't stop hazards");
        }

        [Test]
        public void Knockback_OntoHazard_DealsHazardDamage()
        {
            CombatGrid g = MakeGrid();
            Floor(g, 4).Hazard = CombatRules.HazardDamage;
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 2), leader: true);
            Unit foe = s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 3));
            s.Start();

            Assert.IsTrue(s.TryUseSkill(me, 1, Floor(g, 3)), "Shove");
            Assert.AreEqual(Floor(g, 5), foe.Tile, "pushed two columns, across the hazard");
            Assert.AreEqual(CombatPresets.Enforcer.MaxHp - 1 - CombatRules.HazardDamage, foe.Hp, "shove 1 + hazard 2 on the first step");
        }

        [Test]
        public void Leader_KilledByHazard_LosesTheFight()
        {
            CombatGrid g = MakeGrid();
            Floor(g, 3).Hazard = CombatRules.HazardDamage;
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true);
            s.AddUnit(CombatPresets.Enforcer, Team.Enemy, Floor(g, 9));
            s.Start();
            me.Hp = 2;
            Assert.IsTrue(s.TryMove(me, Floor(g, 3)));
            Assert.AreEqual(Phase.Defeat, s.Phase);
        }

        [Test]
        public void Enemy_PrefersAFiringSpotOffTheHazard()
        {
            Tile Pick(bool hazard)
            {
                CombatGrid g = MakeGrid();
                if (hazard) Floor(g, 8).Hazard = CombatRules.HazardDamage;
                CombatState s = Battle(g);
                s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 3), leader: true);
                Unit sentry = s.AddUnit(CombatPresets.Sentry, Team.Enemy, Floor(g, 9));
                s.Start();
                Assert.IsNotNull(sentry.Intent.Skill, "a shot is available");
                return sentry.Intent.MoveTo;
            }
            CombatGrid probe = MakeGrid();
            Assert.AreEqual(Floor(probe, 8).Id, Pick(false).Id, "without the hazard it takes the longer, cheaper shot");
            Assert.AreEqual(Floor(probe, 7).Id, Pick(true).Id, "with it, it steps one further");
        }

        [Test]
        public void Progression_LevelsEveryFiftyXp_AndCaps()
        {
            Assert.AreEqual(1, Progression.Level(0));
            Assert.AreEqual(1, Progression.Level(49));
            Assert.AreEqual(2, Progression.Level(50));
            Assert.AreEqual(Progression.MaxLevel, Progression.Level(100000));
            Assert.AreEqual(0, Progression.BonusHp(10));
            Assert.AreEqual(Progression.HpPerLevel, Progression.BonusHp(50));
            Assert.AreEqual(25, Progression.ToNext(75));
            Assert.AreEqual(0, Progression.ToNext(100000));
        }

        [Test]
        public void BonusHp_RaisesMaxHpForThatUnitOnly()
        {
            CombatGrid g = MakeGrid();
            CombatState s = Battle(g);
            Unit me = s.AddUnit(CombatPresets.Subject, Team.Player, Floor(g, 1), leader: true, bonusHp: 4);
            Assert.AreEqual(CombatPresets.Subject.MaxHp + 4, me.MaxHp);
            Assert.AreEqual(me.MaxHp, me.Hp);
            Assert.AreEqual(8, CombatPresets.Subject.MaxHp, "the shared def is untouched");
        }
    }
}
