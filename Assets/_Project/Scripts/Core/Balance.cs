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

        /// <summary>Motion samples sent to the outputs per second (camera, haptics, recorder, future platform).</summary>
        public const float MotionSamplesPerSecond = 20f;

        // ---------- Elevator and AR / VR transition ----------
        /// <summary>
        /// Seconds the elevator takes to build itself around the player (floor, walls, roof) before the real
        /// room fades out. Slow on purpose: watching the cabin close in the real room is the AR to VR transition.
        /// </summary>
        public const float ElevatorCloseSeconds = 8f;

        /// <summary>Passthrough fade between the real room and the tower (short, the cabin hides the change).</summary>
        public const float PassthroughFadeSeconds = 1.5f;

        /// <summary>Length of the ride to the top of the tower (design: 15 to 20 s of build-up).</summary>
        public const float AscentSeconds = 15f;

        /// <summary>Seconds for the cabin to open and sink under the player's feet when arriving at the top.</summary>
        public const float ArrivalSeconds = 5f;

        /// <summary>Length of the emergency descent after the collapse (comfort: at most 3 to 5 s of falling).</summary>
        public const float RescueSeconds = 5f;

        /// <summary>The fall ends in black, the real room is switched on behind it, then the black clears.</summary>
        public const float FadeToBlackSeconds = 0.6f;
        public const float FadeFromBlackSeconds = 1.2f;

        // ---------- Weapon ----------
        public const float ShotsPerSecond = 8f;
        public const float HeatPerShot = 0.06f;
        public const float CoolingPerSecond = 0.35f;
        public const float OverheatLockSeconds = 2f;
        public const float HeatAfterOverheat = 0.5f;

        /// <summary>Radius of the shot's sphere cast (meters): a slight aim assist, so a near miss still hits.</summary>
        public const float AimAssistRadius = 0.15f;

        /// <summary>Reach of a shot (meters). Ships fly 15 to 40 m away, so a fair shot is never cut short.</summary>
        public const float WeaponRange = 200f;

        /// <summary>Damage of one shot (a fighter falls with one hit, a bomber needs three).</summary>
        public const int ShotDamage = 1;

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

        /// <summary>
        /// Half width of the square tower (meters). Used to find impact points on the facade. The tower is
        /// barely wider than the 1 x 1 m elevator, so the drop starts right at the player's feet (vertigo).
        /// </summary>
        public const float TowerHalfWidth = 0.6f;

        /// <summary>
        /// Height range (meters, relative to the player platform) where enemy shots hit the facade. Always below the
        /// player's feet, so the impact is visible when looking down and never lands above the top of the tower.
        /// </summary>
        public const float MinImpactHeight = -20f;
        public const float MaxImpactHeight = -1.5f;

        // ---------- Score ----------
        public const int PointsPerSecond = 10;
        public const int PointsPerKill = 5;
        public const int AccuracyBonus = 1000;
        public const int HighScoreEntries = 10;
    }
}
