using AlienInvaders.Core;
using AlienInvaders.Core.Motion;
using UnityEngine;

namespace AlienInvaders.Motion
{
    /// <summary>
    /// Shows the tower's motion with the virtual world. The player's platform and the tower under their feet never
    /// move (stable frame, comfort rule); instead the city, the ground and the horizon rotate around the base of the
    /// tower, which is what someone riding a leaning tower would see: the tower stays straight under their feet and
    /// the city leans the other way. Drops lift the city (the player sinks) and shaking adds a small tremble.
    /// Samples arrive at 20 Hz, so they are smoothed to the display frame rate.
    /// Lives on TiltPivot.
    /// </summary>
    public class CameraMotionOutput : MonoBehaviour, IMotionOutput
    {
        [Tooltip("Base of the tower in the parent's space: the city leans around this point.")]
        [SerializeField] private Vector3 _pivotPoint = new Vector3(0f, -60.3f, 0f);
        [Tooltip("Seconds to catch up with a new sample (a new one arrives every 0.05 s).")]
        [SerializeField] private float _smoothing = 0.08f;

        [Header("Tremble at full shake")]
        [SerializeField] private float _maxShakeDegrees = 0.5f;
        [SerializeField] private float _maxShakeMeters = 0.03f;
        [SerializeField] private float _shakeFrequency = 9f;

        private MotionSample _target;
        private float _pitch;
        private float _roll;
        private float _heave;
        private float _shake;

        /// <summary>Stores the newest sample; LateUpdate eases towards it.</summary>
        public void Apply(MotionSample sample)
        {
            _target = sample;
        }

        /// <summary>Eases the world back to level (end of the match).</summary>
        public void ResetToLevel()
        {
            _target = MotionSample.Level;
        }

        private void LateUpdate()
        {
            // Exponential smoothing: the same feel at any frame rate.
            float blend = 1f - Mathf.Exp(-Time.deltaTime / _smoothing);
            _pitch = Mathf.Lerp(_pitch, _target.Pitch, blend);
            _roll = Mathf.Lerp(_roll, _target.Roll, blend);
            _heave = Mathf.Lerp(_heave, _target.Heave, blend);
            _shake = Mathf.Lerp(_shake, _target.Shake, blend);
            ApplyToTransform();
        }

        private void ApplyToTransform()
        {
            // Leaning towards North tips the player forward, so for them the northern horizon rises: a negative
            // rotation around X lifts +Z. Leaning towards East lifts the eastern horizon: positive rotation around Z.
            Quaternion tilt = Quaternion.Euler(-_pitch, 0f, _roll);
            // Rotate around the base of the tower instead of around this transform's origin (the player's feet).
            Vector3 tiltOffset = _pivotPoint - tilt * _pivotPoint;

            Quaternion tremble = Quaternion.identity;
            Vector3 trembleOffset = Vector3.zero;
            if (_shake > 0.001f)
            {
                // Perlin noise instead of random values: a smooth tremble, never a jittery jump.
                float t = Time.time * _shakeFrequency;
                float degrees = _maxShakeDegrees * _shake;
                tremble = Quaternion.Euler(Noise(t, 0f) * degrees, 0f, Noise(t, 10f) * degrees);
                trembleOffset = new Vector3(Noise(t, 20f), Noise(t, 30f), Noise(t, 40f)) * (_maxShakeMeters * _shake);
            }

            // The tremble turns around the player; a drop (negative heave) lifts the world by the same amount.
            transform.localRotation = tremble * tilt;
            transform.localPosition = tremble * tiltOffset + trembleOffset - Vector3.up * _heave;
        }

        // Smooth noise between -1 and 1; the offset picks an independent curve for each axis.
        private static float Noise(float time, float offset)
        {
            return Mathf.PerlinNoise(time, offset) * 2f - 1f;
        }
    }
}
