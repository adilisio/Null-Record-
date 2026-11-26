using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>What the player has collected: access level, hazard protection, tools, scrap and story flags.</summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(0)] private int keycardLevel;
        [SerializeField] private HazardType protections = HazardType.None;
        [SerializeField, Min(0)] private int scrap;
        [SerializeField, Min(0)] private int xp;

        private readonly HashSet<string> _tools = new HashSet<string>();
        private readonly HashSet<string> _flags = new HashSet<string>();

        public int KeycardLevel => keycardLevel;
        public HazardType Protections => protections;
        public int Scrap => scrap;
        /// <summary>Party XP from encounters (see <c>Game.Combat.Progression</c>).</summary>
        public int Xp => xp;

        public event Action Changed;

        public void GrantKeycard(int level)
        {
            if (level <= keycardLevel) return;
            keycardLevel = level;
            Changed?.Invoke();
        }

        public bool IsProtectedFrom(HazardType hazard) => hazard == HazardType.None || (protections & hazard) == hazard;

        public void GrantProtection(HazardType hazard)
        {
            if (IsProtectedFrom(hazard)) return;
            protections |= hazard;
            Changed?.Invoke();
        }

        public bool HasTool(string toolId) => _tools.Contains(toolId);

        public void GrantTool(string toolId)
        {
            if (!string.IsNullOrEmpty(toolId) && _tools.Add(toolId)) Changed?.Invoke();
        }

        public void AddScrap(int amount)
        {
            scrap = Mathf.Max(0, scrap + amount);
            Changed?.Invoke();
        }

        public void AddXp(int amount)
        {
            xp = Mathf.Max(0, xp + amount);
            Changed?.Invoke();
        }

        public IEnumerable<string> Tools => _tools;
        public IEnumerable<string> Flags => _flags;

        /// <summary>Replaces everything at once (save loading). Raises <see cref="Changed"/> once.</summary>
        public void Restore(int keycard, HazardType hazards, int scrapAmount, IEnumerable<string> tools, IEnumerable<string> flags, int xpAmount = 0)
        {
            xp = Mathf.Max(0, xpAmount);
            keycardLevel = Mathf.Max(0, keycard);
            protections = hazards;
            scrap = Mathf.Max(0, scrapAmount);
            _tools.Clear();
            _flags.Clear();
            if (tools != null) _tools.UnionWith(tools);
            if (flags != null) _flags.UnionWith(flags);
            Changed?.Invoke();
        }

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (_flags.Add(flag)) Changed?.Invoke();
        }
    }
}
