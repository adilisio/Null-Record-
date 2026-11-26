using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>Trigger that updates the player's respawn point. Autosave will hook in here later.</summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Where the player reappears. Defaults to this transform's position at floor level.")]
        [SerializeField] private Transform spawnPoint;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            PlayerRespawn respawn = other.GetComponent<PlayerRespawn>();
            if (respawn == null) return;
            respawn.SetCheckpoint(spawnPoint != null ? spawnPoint.position : transform.position);
        }
    }
}
