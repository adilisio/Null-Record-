using UnityEngine;

namespace Game.World
{
    /// <summary>Spins its transform around a local axis (alarm beacons, fans).</summary>
    public class Rotator : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 180f;

        public void Configure(Vector3 localAxis, float speed)
        {
            axis = localAxis;
            degreesPerSecond = speed;
        }

        private void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
