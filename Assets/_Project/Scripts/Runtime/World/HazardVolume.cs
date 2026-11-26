using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>Trigger volume that sends an unprotected player back to the last checkpoint.</summary>
    [RequireComponent(typeof(Collider))]
    public class HazardVolume : MonoBehaviour
    {
        [SerializeField] private HazardType hazard = HazardType.Heat;
        [SerializeField] private string deathMessage = "Scalded by venting steam.";

        public HazardType Hazard => hazard;

        public void Configure(HazardType type, string message)
        {
            hazard = type;
            deathMessage = message;
        }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerStay(Collider other)
        {
            PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
            if (respawn == null) return;
            PlayerInventory inv = other.GetComponent<PlayerInventory>();
            if (inv != null && inv.IsProtectedFrom(hazard)) return;
            respawn.Kill($"{deathMessage} ({hazard.ProtectionName()} required)");
        }
    }
}
