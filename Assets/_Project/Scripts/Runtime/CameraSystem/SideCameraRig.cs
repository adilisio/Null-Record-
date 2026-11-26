using Game.Combat;
using Game.Player;
using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Side-on follow camera. Looks down +Z at the play lane, leads in the facing direction,
    /// and keeps the visible area inside the active <see cref="CameraZone"/>.
    /// During an encounter it frames the whole encounter area instead, pulling back if needed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SideCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 16f;
        [SerializeField] private float heightOffset = 1.5f;
        [SerializeField] private float lookAhead = 2f;
        [SerializeField] private float lookAheadSmoothTime = 0.6f;
        [SerializeField] private float followSmoothTimeX = 0.2f;
        [SerializeField] private float followSmoothTimeY = 0.35f;
        [Tooltip("Seconds to ease into a new zone's bounds.")]
        [SerializeField] private float zoneBlendTime = 0.5f;

        private Camera _camera;
        private PlayerMotor _motor;
        private Vector2 _focus;
        private Vector2 _velocity;
        private float _lead;
        private float _leadVelocity;
        private Rect _bounds;
        private bool _hasBounds;
        private CameraZone _zone;
        private float _distance;
        private float _distanceVelocity;

        public CameraZone ActiveZone => _zone;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _motor = target != null ? target.GetComponent<PlayerMotor>() : null;
            SnapToTarget();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _distance = distance;
            transform.rotation = Quaternion.identity;
            SetTarget(target);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            float desiredLead = _motor != null ? _motor.Facing * lookAhead : 0f;
            _lead = Mathf.SmoothDamp(_lead, desiredLead, ref _leadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);

            Vector2 desired = new Vector2(target.position.x + _lead, target.position.y + heightOffset);
            float desiredDistance = distance;
            EncounterController fight = EncounterController.Active;
            if (fight != null && _camera != null)
            {
                // Frame the encounter area with a little margin, centred on it.
                Rect a = fight.Area;
                float tan = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float fitW = (a.width * 0.5f + 1f) / (tan * _camera.aspect);
                float fitH = (a.height * 0.5f + 1f) / tan;
                desiredDistance = Mathf.Max(distance, fitW, fitH);
                // With touch controls up, sit lower so the grid rides above the stick and buttons.
                desired = a.center + new Vector2(0f, Game.UI.TouchControls.ShouldShow ? -1.3f : 0.5f);
            }
            else
            {
                UpdateBounds(desired, dt);
                desired = Clamp(desired);
            }
            _distance = Mathf.SmoothDamp(_distance, desiredDistance, ref _distanceVelocity, 0.4f, Mathf.Infinity, dt);

            _focus.x = Mathf.SmoothDamp(_focus.x, desired.x, ref _velocity.x, followSmoothTimeX, Mathf.Infinity, dt);
            _focus.y = Mathf.SmoothDamp(_focus.y, desired.y, ref _velocity.y, followSmoothTimeY, Mathf.Infinity, dt);
            Apply();
        }

        private void SnapToTarget()
        {
            if (target == null) return;
            _lead = _motor != null ? _motor.Facing * lookAhead : 0f;
            Vector2 p = new Vector2(target.position.x + _lead, target.position.y + heightOffset);
            _zone = CameraZone.Find(target.position);
            _hasBounds = _zone != null;
            if (_hasBounds) _bounds = _zone.Rect;
            _focus = Clamp(p);
            _velocity = Vector2.zero;
            Apply();
        }

        private void UpdateBounds(Vector2 desired, float dt)
        {
            CameraZone zone = CameraZone.Find(target.position);
            if (zone != null) _zone = zone;
            if (_zone == null)
            {
                _hasBounds = false;
                return;
            }

            Rect goal = _zone.Rect;
            if (!_hasBounds)
            {
                _bounds = goal;
                _hasBounds = true;
                return;
            }

            // Ease the bounds rather than snapping so room transitions pan smoothly.
            float t = zoneBlendTime <= 0f ? 1f : 1f - Mathf.Exp(-dt * 4f / zoneBlendTime);
            _bounds = new Rect(
                Mathf.Lerp(_bounds.x, goal.x, t),
                Mathf.Lerp(_bounds.y, goal.y, t),
                Mathf.Lerp(_bounds.width, goal.width, t),
                Mathf.Lerp(_bounds.height, goal.height, t));
        }

        private Vector2 Clamp(Vector2 p)
        {
            if (!_hasBounds || _camera == null) return p;

            float halfH = distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _camera.aspect;

            p.x = _bounds.width <= halfW * 2f ? _bounds.center.x : Mathf.Clamp(p.x, _bounds.xMin + halfW, _bounds.xMax - halfW);
            p.y = _bounds.height <= halfH * 2f ? _bounds.center.y : Mathf.Clamp(p.y, _bounds.yMin + halfH, _bounds.yMax - halfH);
            return p;
        }

        private void Apply()
        {
            float laneZ = _motor != null ? _motor.LaneZ : 0f;
            transform.position = new Vector3(_focus.x, _focus.y, laneZ - (_distance > 0f ? _distance : distance));
        }
    }
}
