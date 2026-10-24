using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Fast, fragile ship that darts like an alien craft: often it splits its way to the station into two straight
    /// dashes, stopping for an instant at a turning point off to one side, so it is hard to lead with the weapon.
    /// Low damage, but it appears in large numbers.
    /// </summary>
    public class Fighter : EnemyShip
    {
        public Fighter(Vector3 spawnPosition, WaveConfig wave, Random random, Airspace airspace = null)
            : base(spawnPosition, wave.CityRuns, random, airspace)
        {
            Type = ShipType.Fighter;
            Health = Balance.FighterHealth;
            MaxHealth = Health;
            Speed = Balance.FighterSpeed * wave.SpeedMultiplier;
            LaunchSpeed = Balance.FighterLaunchSpeed;
            Acceleration = Balance.FighterAcceleration * wave.SpeedMultiplier;
            DamageToTower = Balance.FighterDamage;
            AimTime = wave.AimTimeFighter;
        }

        /// <summary>
        /// Around the tower when it must; otherwise, often, a turning point beside the middle of the way, always on
        /// the side away from the tower, so the two dashes make a wide V.
        /// </summary>
        protected override bool PickTurningPoint(Vector3 from, Vector3 to, out Vector3 turningPoint)
        {
            if (AroundTheTower(from, to, out turningPoint))
            {
                return true;
            }
            Vector3 middle = (from + to) * 0.5f;
            Vector3 side = Vector3.Cross(to - from, Vector3.UnitY);
            if (Random.NextDouble() >= Balance.FighterDoglegChance || side.LengthSquared() < 1f)
            {
                return false;
            }
            side = Vector3.Normalize(side);
            if (side.X * middle.X + side.Z * middle.Z < 0f)
            {
                side = -side;
            }
            turningPoint = middle + side * Lerp(0.3f, 1f, (float)Random.NextDouble()) * Balance.FighterDoglegOffset;
            return true;
        }
    }
}
