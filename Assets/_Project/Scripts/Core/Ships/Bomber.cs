using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Slow, armored ship that flies straight to its station and fires a heavy shot.
    /// It takes a long burst, so the player must decide whether to focus it or the fighters.
    /// </summary>
    public class Bomber : EnemyShip
    {
        public Bomber(Vector3 spawnPosition, WaveConfig wave, Random random)
            : base(spawnPosition, random)
        {
            Type = ShipType.Bomber;
            Health = Balance.BomberHealth;
            MaxHealth = Health;
            Speed = Balance.BomberSpeed * wave.SpeedMultiplier;
            DamageToTower = Balance.BomberDamage;
            AimTime = wave.AimTimeBomber;
        }

        /// <summary>Straight-line flight: predictable, but hard to stop in time.</summary>
        protected override Vector3 ComputeNextPosition(Vector3 current, Vector3 destination, float deltaTime)
        {
            return MoveTowards(current, destination, Speed * deltaTime);
        }
    }
}
