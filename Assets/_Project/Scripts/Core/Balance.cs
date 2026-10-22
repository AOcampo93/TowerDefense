namespace AlienInvaders.Core
{
    /// <summary>
    /// Every tunable number of the game lives here, so balancing never requires touching
    /// the game logic. Values are starting points from the design document (section 5).
    /// </summary>
    public static class Balance
    {
        // ---------- Tower integrity ----------
        public const float MaxIntegrity = 100f;

        /// <summary>Degrees of tilt per point of net (unbalanced) damage.</summary>
        public const float TiltPerDamagePoint = 0.4f;

        /// <summary>Maximum tilt speed in degrees per second (comfort + actuator friendly).</summary>
        public const float MaxTiltSpeed = 5f;

        /// <summary>Integrity thresholds that trigger structural drops (and finally the rescue).</summary>
        public static readonly int[] IntegrityThresholds = { 75, 50, 25, 0 };

        /// <summary>Visual drop of the tower (meters) for the 75 %, 50 % and 25 % thresholds.</summary>
        public static readonly float[] DropDistances = { 1.0f, 1.5f, 2.0f };

        /// <summary>Duration of each drop in seconds (short drops are more comfortable in VR).</summary>
        public const float DropDuration = 0.4f;

        /// <summary>Integrity at or below which the game enters the Critical state.</summary>
        public const float CriticalIntegrity = 25f;

        // ---------- Weapon ----------
        public const float ShotsPerSecond = 8f;
        public const float HeatPerShot = 0.06f;
        public const float CoolingPerSecond = 0.35f;
        public const float OverheatLockSeconds = 2f;
        public const float HeatAfterOverheat = 0.5f;

        // ---------- Waves ----------
        public const int MaxPortals = 4;
        public const float MaxSpeedMultiplier = 2f;
        public const float SpeedIncreasePerWave = 0.08f;
        public const float AimTimeDecreasePerWave = 0.12f;
        public const float MinAimTime = 0.6f;
        public const float BomberRatioPerWave = 0.05f;
        public const float MaxBomberRatio = 0.4f;
        public const float PeaceSeconds = 3f;
        public const float PortalOpeningSeconds = 5f;
        public const float SpawnIntervalSeconds = 0.6f;

        // ---------- Ships ----------
        public const int FighterHealth = 1;
        public const float FighterSpeed = 6f;
        public const float FighterAimTime = 1.5f;
        public const float FighterDamage = 2f;

        public const int BomberHealth = 3;
        public const float BomberSpeed = 3f;
        public const float BomberAimTime = 2.5f;
        public const float BomberDamage = 6f;

        /// <summary>Flight zone around the tower (meters, relative to the player platform).</summary>
        public const float MinFlightRadius = 15f;
        public const float MaxFlightRadius = 40f;
        public const float MinFlightHeight = -5f;
        public const float MaxFlightHeight = 15f;

        /// <summary>Half width of the square tower (meters). Used to find impact points on the facade.</summary>
        public const float TowerHalfWidth = 6f;

        // ---------- Score ----------
        public const int PointsPerSecond = 10;
        public const int PointsPerKill = 5;
        public const int AccuracyBonus = 1000;
        public const int HighScoreEntries = 10;
    }
}
