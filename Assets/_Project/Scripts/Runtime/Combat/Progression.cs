using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// XP and levels. First pass: one shared party XP pool (the GDD wants per-unit XP later);
    /// each level gives the Subject +1 max HP (see PlayerCombat.MaxHpFor). Skill choices come later.
    /// </summary>
    public static class Progression
    {
        public const int XpPerLevel = 50;
        public const int MaxLevel = 5;
        public const int HpPerLevel = 1;

        public static int Level(int xp) => Mathf.Clamp(1 + Mathf.Max(0, xp) / XpPerLevel, 1, MaxLevel);

        public static int BonusHp(int xp) => (Level(xp) - 1) * HpPerLevel;

        /// <summary>XP still needed for the next level, or 0 at the cap.</summary>
        public static int ToNext(int xp) => Level(xp) >= MaxLevel ? 0 : Level(xp) * XpPerLevel - Mathf.Max(0, xp);
    }
}
