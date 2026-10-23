using AlienInvaders.Core;
using AlienInvaders.Core.Motion;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Motion
{
    /// <summary>
    /// Lets the hands feel the tower. Impacts and structural drops arrive as jumps in the shake value and become one
    /// pulse on both controllers (drops at full strength). The constant tremble of the critical state becomes a slow
    /// heartbeat on the left controller only, so it never cuts off the weapon's own vibration in the right hand.
    /// Like every motion output it only reads MotionSamples, never game rules.
    /// </summary>
    public class HapticMotionOutput : MonoBehaviour, IMotionOutput
    {
        [Tooltip("Rise in shake between two samples that counts as a new impact.")]
        [SerializeField] private float _impactThreshold = 0.05f;
        [SerializeField] private float _impactSeconds = 0.25f;
        [Tooltip("Shake at or above this value is a structural drop: full strength, longer pulse.")]
        [SerializeField] private float _dropShake = 0.6f;
        [SerializeField] private float _dropSeconds = 0.45f;

        [Header("Critical heartbeat (left hand)")]
        [SerializeField] private float _heartbeatInterval = 0.6f;
        [Tooltip("Pulse amplitude = shake x gain.")]
        [SerializeField] private float _heartbeatGain = 3f;
        [SerializeField] private float _heartbeatSeconds = 0.15f;

        private float _lastShake;
        private float _heartbeatTimer;

        /// <summary>Turns a jump in shake into a pulse on both hands.</summary>
        public void Apply(MotionSample sample)
        {
            if (sample.Shake > _lastShake + _impactThreshold)
            {
                bool drop = sample.Shake >= _dropShake;
                ControllerHaptics.PulseBoth(drop ? 1f : Mathf.Clamp01(sample.Shake * 2f),
                    drop ? _dropSeconds : _impactSeconds);
            }
            _lastShake = sample.Shake;
        }

        /// <summary>Stops the heartbeat.</summary>
        public void ResetToLevel()
        {
            _lastShake = 0f;
        }

        private void Update()
        {
            // While the tower keeps trembling (critical state), a slow pulse reminds the player it is about to fall.
            _heartbeatTimer -= Time.deltaTime;
            if (_heartbeatTimer <= 0f && _lastShake > 0.05f)
            {
                _heartbeatTimer = _heartbeatInterval;
                ControllerHaptics.PulseLeft(Mathf.Clamp01(_lastShake * _heartbeatGain), _heartbeatSeconds);
            }
        }
    }
}
