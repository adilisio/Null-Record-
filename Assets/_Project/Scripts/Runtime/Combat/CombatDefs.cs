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

    /// <summary>Prototype roster. Numbers are first-pass and meant to be tuned.</summary>
    public static class CombatPresets
    {
        public static readonly SkillDef Strike = new SkillDef { Name = "Strike", Damage = 2 };
        public static readonly SkillDef Shove = new SkillDef { Name = "Shove", Damage = 1, Knockback = 2, Cooldown = 2 };
        public static readonly SkillDef Baton = new SkillDef { Name = "Baton", Damage = 3 };
        public static readonly SkillDef ShieldBash = new SkillDef { Name = "Shield Bash", Damage = 1, Knockback = 2, Cooldown = 2 };
        public static readonly SkillDef StunProd = new SkillDef { Name = "Stun Prod", Damage = 3 };
        public static readonly SkillDef TaserShot = new SkillDef { Name = "Taser Shot", Ranged = true, MinRange = 2, MaxRange = 5, Damage = 2 };

        /// <summary>The protagonist. Losing them loses the encounter.</summary>
        public static readonly UnitDef Subject = new UnitDef { Name = "Subject", MaxHp = 8, Move = 4, Skills = new[] { Strike, Shove } };

        /// <summary>Companion 1 (frontline). Stand-in until the recruit scene exists.</summary>
        public static readonly UnitDef Guard = new UnitDef { Name = "Guard", MaxHp = 12, Move = 3, Armor = 1, Skills = new[] { Baton, ShieldBash } };

        public static readonly UnitDef Enforcer = new UnitDef { Name = "Enforcer", MaxHp = 6, Move = 3, Skills = new[] { StunProd } };
        public static readonly UnitDef Sentry = new UnitDef { Name = "Sentry", MaxHp = 4, Move = 2, Skills = new[] { TaserShot } };

        public static UnitDef Find(string name)
        {
            switch (name)
            {
                case "Subject": return Subject;
                case "Guard": return Guard;
                case "Enforcer": return Enforcer;
                case "Sentry": return Sentry;
                default: return null;
            }
        }
    }
}
