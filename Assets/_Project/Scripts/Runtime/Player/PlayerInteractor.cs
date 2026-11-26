using System;
using Game.World;
using UnityEngine;

namespace Game.Player
{
    /// <summary>Finds the nearest <see cref="Interactable"/> in range and uses it on the Interact button.</summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Height above the feet that distance is measured from.")]
        [SerializeField] private float probeHeight = 0.9f;

        private PlayerInputReader _input;
        private PlayerMotor _motor;

        public PlayerInventory Inventory { get; private set; }
        public PlayerMotor Motor => _motor;
        public Interactable Current { get; private set; }

        public event Action<Interactable> CurrentChanged;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _motor = GetComponent<PlayerMotor>();
            Inventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            bool canAct = _motor == null || (_motor.ControlEnabled && _motor.State == MotorState.Grounded);
            Interactable next = canAct ? FindNearest() : null;
            if (next != Current)
            {
                Current = next;
                CurrentChanged?.Invoke(Current);
            }

            if (Current != null && _input.InteractPressed)
            {
                Current.Interact(this);
            }
        }

        private Interactable FindNearest()
        {
            Vector3 probe = transform.position + Vector3.up * probeHeight;
            Interactable best = null;
            float bestSqr = float.MaxValue;
            var all = Interactable.Registered;
            for (int i = 0; i < all.Count; i++)
            {
                Interactable it = all[i];
                Vector3 d = it.Point - probe;
                d.z = 0f; // side-on: ignore depth
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr && it.InReach(probe, sqr) && it.IsAvailable(this))
                {
                    best = it;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
