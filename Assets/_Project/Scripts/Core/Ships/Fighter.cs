using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Fast, fragile ship that zigzags on its way to the station, making it harder to hit.
    /// Low damage, but it appears in large numbers.
    /// </summary>
    public class Fighter : EnemyShip
    {
        private const float ZigzagAmplitude = 6f; // meters
        private const float ZigzagFrequency = 1.5f; // oscillations per second

        private float _time;

        public Fighter(Vector3 spawnPosition, WaveConfig wave, Random random)
            : base(spawnPosition, random)
        {
            Type = ShipType.Fighter;
            Health = Balance.FighterHealth;
            MaxHealth = Health;
            Speed = Balance.FighterSpeed * wave.SpeedMultiplier;
            DamageToTower = Balance.FighterDamage;
            AimTime = wave.AimTimeFighter;
            _time = (float)random.NextDouble() * 10f; // so fighters do not zigzag in sync
        }

        /// <summary>
        /// Moves forward and adds a sideways sine offset that fades out near the station,
        /// so the ship still stops exactly where it aims.
        /// </summary>
        protected override Vector3 ComputeNextPosition(Vector3 current, Vector3 destination, float deltaTime)
        {
            _time += deltaTime;

            Vector3 forward = MoveTowards(current, destination, Speed * deltaTime);
            Vector3 direction = destination - current;
            float distance = direction.Length();
            if (distance < 0.0001f)
            {
                return destination;
            }

            // Sideways vector on the horizontal plane (perpendicular to the flight direction).
            Vector3 side = Vector3.Normalize(Vector3.Cross(direction / distance, Vector3.UnitY));
            if (float.IsNaN(side.X))
            {
                return forward; // flying straight up or down: no zigzag
            }

            float fade = Math.Min(1f, distance / 20f);
            float offsetSpeed = (float)Math.Cos(_time * ZigzagFrequency * 2.0 * Math.PI)
                                * ZigzagAmplitude * ZigzagFrequency * fade;
            return forward + side * offsetSpeed * deltaTime;
        }
    }
}
