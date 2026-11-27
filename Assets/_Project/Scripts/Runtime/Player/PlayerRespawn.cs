using System.Collections;
using Game.Core;
using Game.Audio;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Death (hazards, falls, or HP running out in a fight): brief pause, then back to the last checkpoint.
    /// <see cref="Respawned"/> lets fights reset and HP refill.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 0.6f;

        private PlayerMotor _motor;
        private Vector3 _checkpoint;
        private bool _respawning;

        public Vector3 Checkpoint => _checkpoint;
        public bool Respawning => _respawning;

        /// <summary>Raised after the player is back at the checkpoint.</summary>
        public static event System.Action Respawned;

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
            GameAudio.Play(SoundId.Hurt, transform.position);
            yield return new WaitForSeconds(respawnDelay);
            _motor.Teleport(_checkpoint);
            Game.UI.ScreenFade.FromBlack(0.4f);
            _motor.ControlEnabled = true;
            _respawning = false;
            Respawned?.Invoke();
        }
    }
}
