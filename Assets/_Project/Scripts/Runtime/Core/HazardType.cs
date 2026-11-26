using System;

namespace Game.Core
{
    /// <summary>Environmental hazards that gate the world. Protections are earned, not jumped past.</summary>
    [Flags]
    public enum HazardType
    {
        None = 0,
        Heat = 1 << 0,      // steam, reactor: thermal lining
        Gas = 1 << 1,       // contaminated air: filter mask
        Electric = 1 << 2,  // live panels, charged water: insulated gloves
    }

    public static class HazardTypeExtensions
    {
        public static string ProtectionName(this HazardType hazard)
        {
            switch (hazard)
            {
                case HazardType.Heat: return "Thermal Lining";
                case HazardType.Gas: return "Filter Mask";
                case HazardType.Electric: return "Insulated Gloves";
                default: return hazard.ToString();
            }
        }
    }
}
