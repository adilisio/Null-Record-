using Game.Player;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>Footsteps, jumps, landings and ladder clanks, driven by the motor's state and movement.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private float strideLength = 0.6f;
        [SerializeField] private float rungSpacing = 0.55f;
        [Tooltip("Fall speed (m/s) above which landing makes a sound.")]
        [SerializeField] private float landSpeed = 5f;

        private PlayerMotor _motor;
        private MotorState _prevState;
        private Vector3 _prevPos;
        private float _prevVy;
        private float _stride;
        private float _climb;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _prevPos = transform.position;
        }

        private void Start() => _prevState = _motor.State;

        private void LateUpdate()
        {
            if (_motor.ExternallyControlled || Time.deltaTime <= 0f)
            {
                _prevPos = transform.position;
                return;
            }

            Vector3 pos = transform.position;
            Vector3 delta = pos - _prevPos;
            MotorState state = _motor.State;
            Vector3 feet = pos;

            if (_prevState == MotorState.Grounded && state == MotorState.Airborne && _motor.Velocity.y > 2f)
            {
                GameAudio.Play(SoundId.Jump, feet);
            }
            if (_prevState == MotorState.Airborne && state == MotorState.Grounded && _prevVy < -landSpeed)
            {
                GameAudio.Play(SoundId.Land, feet, Mathf.Clamp01(-_prevVy / 14f) + 0.3f);
                _stride = 0f;
            }
            if (state == MotorState.LedgeHang && _prevState != MotorState.LedgeHang)
            {
                GameAudio.Play(SoundId.LadderClank, feet + Vector3.up * 1.8f, 0.7f);
            }

            if (state == MotorState.Grounded && Mathf.Abs(_motor.Velocity.x) > 0.5f)
            {
                _stride += Mathf.Abs(delta.x);
                if (_stride >= strideLength)
                {
                    _stride -= strideLength;
                    GameAudio.Play(SoundId.Footstep, feet, _motor.IsCrouching ? 0.45f : 1f);
                }
            }

            if (state == MotorState.Ladder)
            {
                _climb += Mathf.Abs(delta.y);
                if (_climb >= rungSpacing)
                {
                    _climb -= rungSpacing;
                    GameAudio.Play(SoundId.LadderClank, feet + Vector3.up);
                }
            }
            else
            {
                _climb = 0f;
            }

            _prevVy = _motor.Velocity.y;
            _prevState = state;
            _prevPos = pos;
        }
    }
}
