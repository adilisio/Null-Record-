using UnityEngine;

namespace Game.Combat
{
    public enum Team
    {
        Player,
        Enemy,
    }

    /// <summary>A combat skill. Prototype data lives in <see cref="CombatPresets"/>; ScriptableObjects come later.</summary>
    public sealed class SkillDef
    {
        public string Name;
        /// <summary>Melee hits an adjacent column within <see cref="CombatRules.MeleeMaxHeight"/>; ranged needs line of sight.</summary>
        public bool Ranged;
        public int MinRange = 1;
        public int MaxRange = 1;
        public int Damage;
        public int Knockback;
        /// <summary>Turns before it can be used again (0 = every turn).</summary>
        public int Cooldown;
        /// <summary>Character action clip played when it's used: Strike, Shove, Swing or Shoot.</summary>
        public string Anim = "Strike";
        /// <summary>Support skill: targets an ally (not self) instead of an enemy.</summary>
        public bool TargetsAllies;
        /// <summary>HP restored to the ally (support skills).</summary>
        public int Heal;
    }

    public sealed class UnitDef
    {
        public string Name;
        public int MaxHp;
        public int Move;
        /// <summary>Flat damage reduction (damage never drops below 1).</summary>
        public int Armor;
        public SkillDef[] Skills;
    }

    /// <summary>
    /// XP and levels. First pass: one shared party XP pool (the GDD wants per-unit XP later);
    /// each level gives every player unit +<see cref="HpPerLevel"/> max HP. Skill choices come later.
    /// </summary>
    public static class Progression
    {
        public const int XpPerLevel = 50;
        public const int MaxLevel = 5;
        public const int HpPerLevel = 2;

        public static int Level(int xp) => Mathf.Clamp(1 + Mathf.Max(0, xp) / XpPerLevel, 1, MaxLevel);

        public static int BonusHp(int xp) => (Level(xp) - 1) * HpPerLevel;

        /// <summary>XP still needed for the next level, or 0 at the cap.</summary>
        public static int ToNext(int xp) => Level(xp) >= MaxLevel ? 0 : Level(xp) * XpPerLevel - Mathf.Max(0, xp);
    }

    /// <summary>Prototype roster. Numbers are first-pass and meant to be tuned.</summary>
    public static class CombatPresets
    {
        public static readonly SkillDef Strike = new SkillDef { Name = "Strike", Damage = 2 };
        public static readonly SkillDef Shove = new SkillDef { Name = "Shove", Damage = 1, Knockback = 2, Cooldown = 2, Anim = "Shove" };
        public static readonly SkillDef Baton = new SkillDef { Name = "Baton", Damage = 3, Anim = "Swing" };
        public static readonly SkillDef ShieldBash = new SkillDef { Name = "Shield Bash", Damage = 1, Knockback = 2, Cooldown = 2, Anim = "Shove" };
        public static readonly SkillDef StunProd = new SkillDef { Name = "Stun Prod", Damage = 3, Anim = "Swing" };
        public static readonly SkillDef Maul = new SkillDef { Name = "Maul", Damage = 3, Knockback = 1, Anim = "Swing" };
        public static readonly SkillDef StaticDart = new SkillDef { Name = "Static Dart", Ranged = true, MinRange = 2, MaxRange = 4, Damage = 1, Anim = "Shoot" };
        public static readonly SkillDef Patch = new SkillDef { Name = "Patch", Ranged = true, MinRange = 1, MaxRange = 3, Heal = 3, Cooldown = 2, TargetsAllies = true, Anim = "Shove" };
        public static readonly SkillDef TaserShot = new SkillDef { Name = "Taser Shot", Ranged = true, MinRange = 2, MaxRange = 5, Damage = 2, Anim = "Shoot" };

        /// <summary>The protagonist. Losing them loses the encounter.</summary>
        public static readonly UnitDef Subject = new UnitDef { Name = "Subject", MaxHp = 8, Move = 4, Skills = new[] { Strike, Shove } };

        /// <summary>Companion 1, Dace (frontline). Roster key "Guard".</summary>
        public static readonly UnitDef Guard = new UnitDef { Name = "Dace", MaxHp = 12, Move = 3, Armor = 1, Skills = new[] { Baton, ShieldBash } };

        /// <summary>Companion 2 (support), roster key "Researcher". Working role label; name and writing are the owner's call.</summary>
        public static readonly UnitDef Researcher = new UnitDef { Name = "Researcher", MaxHp = 8, Move = 4, Skills = new[] { StaticDart, Patch } };

        public static readonly UnitDef Enforcer = new UnitDef { Name = "Enforcer", MaxHp = 6, Move = 3, Skills = new[] { StunProd } };
        public static readonly UnitDef Sentry = new UnitDef { Name = "Sentry", MaxHp = 4, Move = 2, Skills = new[] { TaserShot } };

        /// <summary>Heavy melee. Armour 2 shrugs off most hits, so knockback, hazards and falls are the way in.</summary>
        public static readonly UnitDef Warden = new UnitDef { Name = "Warden", MaxHp = 9, Move = 2, Armor = 2, Skills = new[] { Maul } };

        public static UnitDef Find(string name)
        {
            switch (name)
            {
                case "Subject": return Subject;
                case "Guard": return Guard;
                case "Researcher": return Researcher;
                case "Enforcer": return Enforcer;
                case "Sentry": return Sentry;
                case "Warden": return Warden;
                default: return null;
            }
        }
    }
}
