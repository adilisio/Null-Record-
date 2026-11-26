using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A climbable column. The transform sits at the bottom centre of the ladder.
    /// No collider needed: the player motor queries the registry directly.
    /// </summary>
    public class Ladder : MonoBehaviour
    {
        private static readonly List<Ladder> All = new List<Ladder>();

        [SerializeField, Min(0.5f)] private float height = 4f;
        [SerializeField, Min(0.1f)] private float grabHalfWidth = 0.5f;
        [Tooltip("Which side the player steps off at the top: +1 = right, -1 = left.")]
        [SerializeField] private int topExitDirection = 1;

        public float X => transform.position.x;
        public float Bottom => transform.position.y;
        public float Top => transform.position.y + height;
        public int TopExitDirection => topExitDirection >= 0 ? 1 : -1;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Is a player whose feet are at <paramref name="feet"/> close enough to use this ladder?</summary>
        public bool Contains(Vector3 feet, float topMargin = 0.3f)
        {
            return Mathf.Abs(feet.x - X) <= grabHalfWidth
                && feet.y >= Bottom - 0.1f
                && feet.y <= Top + topMargin;
        }

        public static Ladder Find(Vector3 feet)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Contains(feet)) return All[i];
            }
            return null;
        }

        public void Configure(float newHeight, int exitDirection)
        {
            height = newHeight;
            topExitDirection = exitDirection;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Vector3 b = transform.position;
            Gizmos.DrawLine(b + Vector3.left * 0.3f, b + Vector3.left * 0.3f + Vector3.up * height);
            Gizmos.DrawLine(b + Vector3.right * 0.3f, b + Vector3.right * 0.3f + Vector3.up * height);
            Gizmos.DrawRay(b + Vector3.up * height, Vector3.right * TopExitDirection);
        }
    }
}
