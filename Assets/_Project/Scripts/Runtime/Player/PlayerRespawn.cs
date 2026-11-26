using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Out-of-combat death: brief pause, then back to the last checkpoint.
    /// Combat defeats are handled by the encounter system, not here.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 0.6f;

        private PlayerMotor _motor;
        private Vector3 _checkpoint;
        private bool _respawning;

        public Vector3 Checkpoint => _checkpoint;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _checkpoint = transform.position;
        }

        public void SetCheckpoint(Vector3 position) => _checkpoint = position;

        public void Kill(string reason)
        {
            if (_respawning) return;
            StartCoroutine(RespawnRoutine(reason));
        }

        private IEnumerator RespawnRoutine(string reason)
        {
            _respawning = true;
            _motor.ControlEnabled = false;
            if (!string.IsNullOrEmpty(reason)) GameMessages.Show(reason);
            yield return new WaitForSeconds(respawnDelay);
            _motor.Teleport(_checkpoint);
            _motor.ControlEnabled = true;
            _respawning = false;
        }
    }
}
