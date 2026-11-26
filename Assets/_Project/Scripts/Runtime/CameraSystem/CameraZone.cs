using System.Collections.Generic;
using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// A rectangle on the X/Y plane that the camera view is kept inside while the player is within it.
    /// Usually one per room. Zones may overlap; the smallest zone containing the player wins.
    /// </summary>
    public class CameraZone : MonoBehaviour
    {
        private static readonly List<CameraZone> All = new List<CameraZone>();

        [SerializeField] private Vector2 size = new Vector2(20f, 8f);

        public Rect Rect
        {
            get
            {
                Vector3 c = transform.position;
                return new Rect(c.x - size.x * 0.5f, c.y - size.y * 0.5f, size.x, size.y);
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void Configure(Vector2 newSize) => size = newSize;

        public static CameraZone Find(Vector2 point)
        {
            CameraZone best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                Rect r = All[i].Rect;
                float area = r.width * r.height;
                if (r.Contains(point) && area < bestArea)
                {
                    best = All[i];
                    bestArea = area;
                }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0.1f));
        }
    }
}
