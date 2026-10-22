using System;

namespace AlienInvaders.Core.Motion
{
    /// <summary>
    /// Turns what happens to the tower (tilt, threshold drops, impacts, critical state) into a
    /// continuous stream of MotionSamples. This keeps all "how should it feel" rules in one
    /// testable place, independent of Unity and of any hardware.
    /// </summary>
    public class TowerMotionModel
    {
        private const float ImpactShake = 0.35f;
        private const float ShakeDecayPerSecond = 1.2f;
        private const float CriticalShake = 0.15f;
        private const float DropShake = 0.8f;

        private readonly TowerIntegrity _tower;
        private float _time;
        private float _impactShake;

        // Drop animation state.
        private float _heave;
        private float _dropFrom;
        private float _dropTo;
        private float _dropElapsed = float.MaxValue;

        public TowerMotionModel(TowerIntegrity tower)
        {
            _tower = tower ?? throw new ArgumentNullException(nameof(tower));
            _tower.OnDamaged += HandleDamaged;
            _tower.OnThresholdReached += HandleThreshold;
        }

        /// <summary>Accumulated vertical drop in meters (negative).</summary>
        public float Heave => _heave;

        /// <summary>Advances the model and returns the sample for this moment.</summary>
        public MotionSample Tick(float deltaTime)
        {
            _time += deltaTime;
            _tower.Tick(deltaTime);

            // Drops: interpolate the heave quickly between the previous and the new height.
            if (_dropElapsed < Balance.DropDuration)
            {
                _dropElapsed += deltaTime;
                float t = Math.Min(1f, _dropElapsed / Balance.DropDuration);
                float eased = t * t; // accelerate like a real fall
                _heave = _dropFrom + (_dropTo - _dropFrom) * eased;
            }

            // Impact shake fades out; the critical state keeps a constant minimum vibration.
            _impactShake = Math.Max(0f, _impactShake - ShakeDecayPerSecond * deltaTime);
            float baseShake = _tower.IsCritical ? CriticalShake : 0f;
            float shake = Math.Min(1f, Math.Max(baseShake, _impactShake));

            return new MotionSample(
                pitch: _tower.CurrentTilt.Y,
                roll: _tower.CurrentTilt.X,
                heave: _heave,
                shake: shake,
                time: _time);
        }

        private void HandleDamaged(TowerSide side, float amount)
        {
            _impactShake = Math.Max(_impactShake, ImpactShake);
        }

        private void HandleThreshold(int threshold)
        {
            // Thresholds 75, 50, 25 map to drops 0, 1, 2. Threshold 0 is the collapse (rescue).
            int index = Array.IndexOf(Balance.IntegrityThresholds, threshold);
            if (index < 0 || index >= Balance.DropDistances.Length)
            {
                return;
            }

            _dropFrom = _heave;
            _dropTo = _heave - Balance.DropDistances[index];
            _dropElapsed = 0f;
            _impactShake = DropShake;
        }

        /// <summary>Clears time, drops and shake for a new match.</summary>
        public void Reset()
        {
            _time = 0f;
            _impactShake = 0f;
            _heave = 0f;
            _dropElapsed = float.MaxValue;
        }
    }
}
