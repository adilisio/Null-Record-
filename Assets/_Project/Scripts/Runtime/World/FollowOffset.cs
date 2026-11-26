using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Keeps this object at a fixed world-space offset from a target, without inheriting its rotation.
    /// Used for the player's soft key light, which must stay on the camera side whichever way they face.
    /// </summary>
    public class FollowOffset : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.4f, -1.6f);

        public void Configure(Transform follow, Vector3 worldOffset)
        {
            target = follow;
            offset = worldOffset;
        }

        private void LateUpdate()
        {
            if (target != null) transform.position = target.position + offset;
        }
    }
}
