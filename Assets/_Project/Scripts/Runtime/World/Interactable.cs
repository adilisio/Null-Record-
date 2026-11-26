using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Base class for anything the player can use with the Interact button.
    /// The player picks the nearest one in range.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> All = new List<Interactable>();
        public static IReadOnlyList<Interactable> Registered => All;

        [SerializeField, Min(0.1f)] private float range = 1.2f;
        [Tooltip("Offset of the interaction point from the transform.")]
        [SerializeField] private Vector3 pointOffset = Vector3.zero;

        public float Range => range;
        public Vector3 Point => transform.position + pointOffset;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Text shown next to the button prompt, e.g. "Open door".</summary>
        public abstract string GetPrompt(PlayerInteractor player);

        public virtual bool IsAvailable(PlayerInteractor player) => isActiveAndEnabled;

        public abstract void Interact(PlayerInteractor player);

        public void SetInteractionPoint(Vector3 offset, float newRange)
        {
            pointOffset = offset;
            range = newRange;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Point, range);
        }
    }
}
