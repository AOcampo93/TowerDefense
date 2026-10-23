using System;
using System.Numerics;

namespace AlienInvaders.Core
{
    /// <summary>
    /// The tower's "health". The player only sees one integrity bar, but internally damage is
    /// stored per facade so the tower can lean towards the side that is being destroyed.
    /// </summary>
    public class TowerIntegrity
    {
        // Damage accumulated on each side, indexed by (int)TowerSide.
        private readonly float[] _damageBySide = new float[4];

        // Unit vectors of each side on the horizontal plane: X = East, Y = North.
        private static readonly Vector2[] SideDirections =
        {
            new Vector2(0f, 1f),   // North
            new Vector2(0f, -1f),  // South
            new Vector2(1f, 0f),   // East
            new Vector2(-1f, 0f)   // West
        };

        // Index of the next threshold in Balance.IntegrityThresholds that has not fired yet.
        private int _nextThresholdIndex;

        /// <summary>Current tilt in degrees (X = towards East, Y = towards North), smoothed over time.</summary>
        public Vector2 CurrentTilt { get; private set; }

        /// <summary>Raised once per threshold (66, 33, 0) in descending order.</summary>
        public event Action<int> OnThresholdReached;

        /// <summary>Raised after every hit with the side and the amount of damage.</summary>
        public event Action<TowerSide, float> OnDamaged;

        /// <summary>Remaining integrity between 0 and 100.</summary>
        public float Integrity
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _damageBySide.Length; i++)
                {
                    total += _damageBySide[i];
                }
                return Math.Max(0f, Balance.MaxIntegrity - total);
            }
        }

        /// <summary>True when the tower has collapsed.</summary>
        public bool IsDestroyed => Integrity <= 0f;

        /// <summary>True when integrity is at or below the critical level.</summary>
        public bool IsCritical => Integrity <= Balance.CriticalIntegrity;

        /// <summary>Damage stored on one side (useful for debugging and tests).</summary>
        public float DamageOn(TowerSide side) => _damageBySide[(int)side];

        /// <summary>
        /// Applies damage to one facade and fires every threshold that was crossed.
        /// Damage after the tower is destroyed is ignored.
        /// </summary>
        public void ReceiveDamage(TowerSide side, float amount)
        {
            if (amount <= 0f || IsDestroyed)
            {
                return;
            }

            _damageBySide[(int)side] += amount;
            OnDamaged?.Invoke(side, amount);

            // A single heavy hit can cross more than one threshold, so use a loop.
            float integrity = Integrity;
            while (_nextThresholdIndex < Balance.IntegrityThresholds.Length &&
                   integrity <= Balance.IntegrityThresholds[_nextThresholdIndex])
            {
                OnThresholdReached?.Invoke(Balance.IntegrityThresholds[_nextThresholdIndex]);
                _nextThresholdIndex++;
            }
        }

        /// <summary>
        /// Maximum tilt allowed for the current damage stage: every floor that gives way lets the tower lean more
        /// (7, 13 and 20 degrees).
        /// </summary>
        public float MaxTiltForStage
        {
            get
            {
                float integrity = Integrity;
                if (integrity > Balance.IntegrityThresholds[0]) return 7f;
                if (integrity > Balance.IntegrityThresholds[1]) return 13f;
                return 20f;
            }
        }

        /// <summary>
        /// Tilt the tower is moving towards. It is the vector sum of the damage on each side:
        /// hits on opposite sides cancel each other, so the tower leans towards the weakest side.
        /// </summary>
        public Vector2 TargetTilt
        {
            get
            {
                Vector2 net = Vector2.Zero;
                for (int i = 0; i < _damageBySide.Length; i++)
                {
                    net += SideDirections[i] * _damageBySide[i];
                }

                float magnitude = net.Length();
                if (magnitude < 0.0001f)
                {
                    return Vector2.Zero;
                }

                float degrees = Math.Min(magnitude * Balance.TiltPerDamagePoint, MaxTiltForStage);
                return Vector2.Normalize(net) * degrees;
            }
        }

        /// <summary>
        /// Moves the current tilt towards the target at a limited angular speed, so the motion
        /// is smooth for the player (and safe for a future actuator platform).
        /// </summary>
        public void Tick(float deltaTime)
        {
            Vector2 toTarget = TargetTilt - CurrentTilt;
            float distance = toTarget.Length();
            float maxStep = Balance.MaxTiltSpeed * deltaTime;

            CurrentTilt = distance <= maxStep
                ? TargetTilt
                : CurrentTilt + Vector2.Normalize(toTarget) * maxStep;
        }

        /// <summary>
        /// Converts a compass azimuth (0 = North, 90 = East) into the facade that faces it.
        /// Each side covers a 90 degree quadrant centered on its direction.
        /// </summary>
        public static TowerSide SideFromAzimuth(float azimuthDegrees)
        {
            float a = ((azimuthDegrees % 360f) + 360f) % 360f; // normalize to 0..360

            if (a >= 315f || a < 45f) return TowerSide.North;
            if (a < 135f) return TowerSide.East;
            if (a < 225f) return TowerSide.South;
            return TowerSide.West;
        }

        /// <summary>Restores the tower for a new match.</summary>
        public void Reset()
        {
            for (int i = 0; i < _damageBySide.Length; i++)
            {
                _damageBySide[i] = 0f;
            }
            _nextThresholdIndex = 0;
            CurrentTilt = Vector2.Zero;
        }
    }
}
