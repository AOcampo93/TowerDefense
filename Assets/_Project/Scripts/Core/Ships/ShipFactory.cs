using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Creates the right subclass for a ship type. The rest of the game only works with the
    /// abstract EnemyShip, so new ship types only need a new subclass and a new case here.
    /// </summary>
    public static class ShipFactory
    {
        public static EnemyShip Create(ShipType type, Vector3 spawnPosition, WaveConfig wave, Random random,
            Airspace airspace = null)
        {
            switch (type)
            {
                case ShipType.Fighter:
                    return new Fighter(spawnPosition, wave, random, airspace);
                case ShipType.Bomber:
                    return new Bomber(spawnPosition, wave, random, airspace);
                default:
                    throw new NotSupportedException($"Ship type {type} is not implemented yet.");
            }
        }
    }
}
