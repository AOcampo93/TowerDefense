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

        /// <summary>
        /// Integrity thresholds where the tower gives way: at 66 % and 33 % a floor collapses and the top drops,
        /// at 0 % the whole tower falls (rescue). Between them the tower only leans with every hit.
        /// </summary>
        public static readonly int[] IntegrityThresholds = { 66, 33, 0 };

        /// <summary>How far the top drops when the 66 % and the 33 % floors give way (meters, about a floor each).</summary>
        public static readonly float[] DropDistances = { 3.5f, 5f };

        /// <summary>Seconds the tower creaks and shakes before a floor gives way, so the drop never comes unannounced.</summary>
        public const float DropWarningSeconds = 0.35f;

        /// <summary>Duration of each drop in seconds.</summary>
        public const float DropDuration = 0.9f;

        /// <summary>Integrity at or below which the game enters the Critical state (after the second floor gives way).</summary>
        public const float CriticalIntegrity = 33f;

        /// <summary>Motion samples sent to the outputs per second (camera, haptics, recorder, future platform).</summary>
        public const float MotionSamplesPerSecond = 20f;

        // ---------- Elevator and AR / VR transition ----------
        /// <summary>
        /// Seconds the elevator takes to build itself around the player (floor, walls, roof) before the real
        /// room fades out. Slow on purpose: watching the cabin close in the real room is the AR to VR transition.
        /// </summary>
        public const float ElevatorCloseSeconds = 14f;

        /// <summary>Passthrough fade between the real room and the tower (short, the cabin hides the change).</summary>
        public const float PassthroughFadeSeconds = 1.5f;

        /// <summary>Length of the ride to the top of the tower (design: 15 to 20 s of build-up).</summary>
        public const float AscentSeconds = 15f;

        /// <summary>Seconds for the cabin to open and sink under the player's feet when arriving at the top.</summary>
        public const float ArrivalSeconds = 5f;

        /// <summary>Length of the emergency descent after the collapse (comfort: at most 3 to 5 s of falling).</summary>
        public const float RescueSeconds = 4f;

        /// <summary>
        /// The hard stop at the bottom: a quick dip to black that hides the switch to the real room behind the closed
        /// cabin. Then the cabin opens in the room like it was built at the start.
        /// </summary>
        public const float LandingDipSeconds = 0.15f;
        public const float LandingRecoverSeconds = 0.5f;

        // ---------- Weapon ----------
        /// <summary>The weapon fires bursts: a held trigger shoots this many bolts per second.</summary>
        public const float ShotsPerSecond = 10f;

        /// <summary>Heat of one bolt: a held trigger overheats the weapon in about three seconds.</summary>
        public const float HeatPerShot = 0.034f;
        public const float CoolingPerSecond = 0.35f;
        public const float OverheatLockSeconds = 2f;
        public const float HeatAfterOverheat = 0.5f;

        /// <summary>Radius of the shot's sphere cast (meters): a slight aim assist, so a near miss still hits.</summary>
        public const float AimAssistRadius = 0.15f;

        /// <summary>Reach of a shot (meters). Ships fly 45 to 95 m away, so a fair shot is never cut short.</summary>
        public const float WeaponRange = 500f;

        /// <summary>Damage of one bolt (a short burst brings down a fighter, a bomber needs a long one).</summary>
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
        /// <summary>Bolts a fighter takes (under a second of burst).</summary>
        public const int FighterHealth = 8;
        public const float FighterSpeed = 16f;
        public const float FighterAimTime = 1.5f;
        public const float FighterDamage = 2f;

        /// <summary>Bolts a bomber takes (about two seconds of burst).</summary>
        public const int BomberHealth = 20;
        public const float BomberSpeed = 8f;
        public const float BomberAimTime = 2.5f;
        public const float BomberDamage = 6f;

        /// <summary>
        /// Flight zone around the tower (meters, relative to the player platform). The ships are huge (a fighter is
        /// about 26 m across, a bomber about 48 m long), so they keep their distance to fit in view.
        /// </summary>
        public const float MinFlightRadius = 45f;
        public const float MaxFlightRadius = 95f;
        public const float MinFlightHeight = -20f;
        public const float MaxFlightHeight = 35f;

        /// <summary>Distance range (meters) where portals open in the sky, beyond the flight zone.</summary>
        public const float MinPortalDistance = 140f;
        public const float MaxPortalDistance = 180f;

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
