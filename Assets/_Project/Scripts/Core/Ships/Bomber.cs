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
        public Bomber(Vector3 spawnPosition, WaveConfig wave, Random random, Airspace airspace = null)
            : base(spawnPosition, wave.CityRuns, random, airspace)
        {
            Type = ShipType.Bomber;
            Health = Balance.BomberHealth;
            MaxHealth = Health;
            Speed = Balance.BomberSpeed * wave.SpeedMultiplier;
            LaunchSpeed = Balance.BomberLaunchSpeed;
            Acceleration = Balance.BomberAcceleration * wave.SpeedMultiplier;
            DamageToTower = Balance.BomberDamage;
            AimTime = wave.AimTimeBomber;
        }

        /// <summary>One slow straight glide, heavy to start and to stop (it only turns to go around the tower):
        /// predictable, but hard to stop in time.</summary>
        protected override bool PickTurningPoint(Vector3 from, Vector3 to, out Vector3 turningPoint)
        {
            return AroundTheTower(from, to, out turningPoint);
        }
    }
}
