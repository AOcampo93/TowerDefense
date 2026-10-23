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

        // Drop animation state. A drop starts after a short warning (the tower creaks and shakes first).
        private float _heave;
        private float _heaveTarget;
        private float _warningLeft;
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
            // Impact shake fades out (new jolts below start again at full strength).
            _impactShake = Math.Max(0f, _impactShake - ShakeDecayPerSecond * deltaTime);

            // A floor about to give way: once the warning is over, the drop starts with the time left in this tick.
            float dropTime = deltaTime;
            if (_warningLeft > 0f)
            {
                _warningLeft -= deltaTime;
                dropTime = 0f;
                if (_warningLeft <= 0f)
                {
                    dropTime = -_warningLeft;
                    _warningLeft = 0f;
                    _dropFrom = _heave;
                    _dropTo = _heaveTarget;
                    _dropElapsed = 0f;
                    _impactShake = DropShake;
                }
            }

            // Drops: interpolate the heave between the previous and the new height, faster and faster.
            if (_dropElapsed < Balance.DropDuration && dropTime > 0f)
            {
                _dropElapsed += dropTime;
                float t = Math.Min(1f, _dropElapsed / Balance.DropDuration);
                float eased = t * t; // accelerate like a real fall
                _heave = _dropFrom + (_dropTo - _dropFrom) * eased;
                if (t >= 1f)
                {
                    // The hard stop when the broken floor lands.
                    _impactShake = DropShake;
                }
            }

            // The critical state keeps a constant minimum vibration.
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
            // Thresholds 66 and 33 map to drops 0 and 1. Threshold 0 is the collapse (rescue).
            int index = Array.IndexOf(Balance.IntegrityThresholds, threshold);
            if (index < 0 || index >= Balance.DropDistances.Length)
            {
                return;
            }

            // Drops add up from the last target, so one heavy hit that crosses two thresholds drops both distances.
            // The tower creaks first; the fall itself starts in Tick when the warning is over.
            _heaveTarget -= Balance.DropDistances[index];
            _warningLeft = Balance.DropWarningSeconds;
            _impactShake = DropShake;
        }

        /// <summary>Clears time, drops and shake for a new match.</summary>
        public void Reset()
        {
            _time = 0f;
            _impactShake = 0f;
            _heave = 0f;
            _heaveTarget = 0f;
            _warningLeft = 0f;
            _dropElapsed = float.MaxValue;
        }
    }
}
