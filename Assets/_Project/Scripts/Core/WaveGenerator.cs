using System;
using System.Collections.Generic;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Creates each wave from a few formulas, so difficulty grows smoothly: more portals,
    /// more ships (and more of them in the air at once), faster ships, less time to react and fewer raids on the city
    /// before the ships turn on the tower. A seed makes every run reproducible,
    /// which allows a future "daily challenge" where everybody plays the same waves.
    /// </summary>
    public class WaveGenerator
    {
        private readonly int _seed;

        /// <summary>Number of the last wave produced by Next() (0 before the first wave).</summary>
        public int CurrentWave { get; private set; }

        public WaveGenerator(int seed)
        {
            _seed = seed;
        }

        /// <summary>Advances to the next wave and returns its configuration.</summary>
        public WaveConfig Next()
        {
            CurrentWave++;
            return Build(CurrentWave, _seed);
        }

        /// <summary>
        /// Pure formula for wave number n (starting at 1). Kept static so it can be tested
        /// and tuned without running the game.
        /// </summary>
        public static WaveConfig Build(int n, int seed)
        {
            if (n < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Waves start at 1.");
            }

            int step = n - 1;
            return new WaveConfig
            {
                Number = n,
                Portals = Math.Min(Balance.MaxPortals, 1 + step / 2), // integer division: 1,1,2,2,3,3,4...
                TotalShips = Balance.FirstWaveShips + Balance.ShipsAddedPerWave * step, // 4, 6, 8, 10...
                MaxShipsInAir = Math.Min(Balance.MaxShipsInAir, Balance.FirstWaveShipsInAir + step), // 3, 4, 5... 8
                CityRuns = Math.Max(Balance.MinCityRuns, Balance.FirstWaveCityRuns - step / Balance.WavesPerCityRunLost), // 3, 3, 3, 2, 2, 2, 1...
                MaxTowerAttackers = Math.Min(Balance.MaxTowerAttackers, Balance.FirstWaveTowerAttackers + step / 2), // 1, 1, 2, 2, 3, 3, 4
                SpeedMultiplier = Math.Min(Balance.MaxSpeedMultiplier, 1f + Balance.SpeedIncreasePerWave * step),
                AimTimeFighter = Math.Max(Balance.MinAimTime, Balance.FighterAimTime - Balance.AimTimeDecreasePerWave * step),
                AimTimeBomber = Math.Max(Balance.MinAimTime, Balance.BomberAimTime - Balance.AimTimeDecreasePerWave * step),
                BomberRatio = Math.Min(Balance.MaxBomberRatio, Balance.BomberRatioPerWave * step),
                Seed = unchecked(seed * 31 + n) // different but deterministic random stream per wave
            };
        }

        /// <summary>
        /// Picks where each portal of the wave opens. Same config (same seed) -> same positions.
        /// Portals are spread around the tower so the player has to turn and use the radar.
        /// </summary>
        public static List<PortalPlacement> PortalPositions(WaveConfig config)
        {
            var random = new Random(config.Seed);
            var result = new List<PortalPlacement>(config.Portals);

            // Start from a random angle and divide the circle evenly, with some jitter.
            float baseAngle = (float)(random.NextDouble() * 360.0);
            float slice = 360f / config.Portals;

            for (int i = 0; i < config.Portals; i++)
            {
                float jitter = (float)(random.NextDouble() - 0.5) * slice * 0.5f;
                result.Add(new PortalPlacement
                {
                    AzimuthDegrees = (baseAngle + slice * i + jitter) % 360f,
                    ElevationDegrees = 4f + (float)random.NextDouble() * 14f,
                    Distance = Balance.MinPortalDistance + (float)random.NextDouble() * (Balance.MaxPortalDistance - Balance.MinPortalDistance)
                });
            }

            return result;
        }

        /// <summary>
        /// Decides the type of every ship in the wave. The share of Bombers grows with the
        /// wave number (none in wave 1, at least one from wave 2); the order is shuffled with the wave seed.
        /// </summary>
        public static List<ShipType> ShipTypes(WaveConfig config)
        {
            int total = config.TotalShips;
            int bombers = (int)Math.Round(total * config.BomberRatio);
            // As soon as the ratio allows bombers (wave 2), at least one shows up, so the new enemy is not delayed
            // until the ratio is big enough to round up to one.
            if (config.BomberRatio > 0f && bombers == 0)
            {
                bombers = 1;
            }
            var types = new List<ShipType>(total);

            for (int i = 0; i < total; i++)
            {
                types.Add(i < bombers ? ShipType.Bomber : ShipType.Fighter);
            }

            // Fisher-Yates shuffle with the wave seed (deterministic).
            var random = new Random(config.Seed + 7);
            for (int i = types.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                ShipType temp = types[i];
                types[i] = types[j];
                types[j] = temp;
            }

            return types;
        }

        /// <summary>Starts again from wave 1 (same seed).</summary>
        public void Reset()
        {
            CurrentWave = 0;
        }
    }
}
