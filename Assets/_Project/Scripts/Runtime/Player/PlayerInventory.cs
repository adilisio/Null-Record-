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

        private readonly HashSet<string> _tools = new HashSet<string>();
        private readonly HashSet<string> _flags = new HashSet<string>();

        public int KeycardLevel => keycardLevel;
        public HazardType Protections => protections;
        public int Scrap => scrap;

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

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (_flags.Add(flag)) Changed?.Invoke();
        }
    }
}
