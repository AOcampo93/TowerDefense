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

        /// <summary>
        /// Length of the emergency descent after the collapse. The cabin goes down at a steady, moderate speed (no free
        /// fall), so the player has time to see the elevator and the city rising around it.
        /// </summary>
        public const float RescueSeconds = 8f;

        /// <summary>
        /// The hard stop at the bottom: a quick dip to black that hides the switch to the real room behind the closed
        /// cabin. Then the cabin opens in the room like it was built at the start.
        /// </summary>
        public const float LandingDipSeconds = 0.15f;
        public const float LandingRecoverSeconds = 0.5f;

        // ---------- Weapon ----------
        /// <summary>The weapon fires bursts: a held trigger shoots this many bolts per second.</summary>
        public const float ShotsPerSecond = 10f;

        /// <summary>
        /// Heat of one bolt: a held trigger overheats the weapon in about four and a half seconds, enough to bring down
        /// several fighters in one burst, so aiming matters more than waiting for the barrel to cool.
        /// </summary>
        public const float HeatPerShot = 0.024f;
        /// <summary>Cooling while the trigger is released: from hot to cold in two seconds.</summary>
        public const float CoolingPerSecond = 0.5f;
        public const float OverheatLockSeconds = 1.5f;
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
        public const float SpeedIncreasePerWave = 0.05f;
        public const float AimTimeDecreasePerWave = 0.08f;
        public const float MinAimTime = 0.6f;
        public const float BomberRatioPerWave = 0.05f;
        public const float MaxBomberRatio = 0.4f;
        public const float PeaceSeconds = 3f;

        /// <summary>
        /// Shortest time from the start of an attack to the end of the calm after it (seconds). A player who clears a
        /// wave quickly earns a longer rest instead of meeting the next, harder wave sooner, so the difficulty grows
        /// with time and skill keeps the tower standing longer.
        /// </summary>
        public const float MinWaveSeconds = 35f;

        /// <summary>
        /// Longest attack (seconds): when the sky is not cleared in time, the next wave opens its portals anyway and the
        /// ships still flying stay. The difficulty keeps growing with time even for a player who cannot keep up.
        /// </summary>
        public const float MaxWaveSeconds = 45f;
        public const float PortalOpeningSeconds = 5f;

        /// <summary>Seconds between two ships coming out of the same portal, so each one has flown clear of it.</summary>
        public const float SpawnIntervalSeconds = 2.5f;

        /// <summary>Ships in the first wave; every wave brings this many more.</summary>
        public const int FirstWaveShips = 4;
        public const int ShipsAddedPerWave = 2;

        /// <summary>
        /// Most ships flying at the same time: 3 in the first wave, one more every wave, up to 8. The rest wait in
        /// their portals, so a wave lasts longer instead of crowding the sky, and a skilled player who shoots them
        /// down quickly is the one who keeps the sky clear.
        /// </summary>
        public const int FirstWaveShipsInAir = 3;
        public const int MaxShipsInAir = 8;

        /// <summary>
        /// Distance (meters) a ship keeps from the others when it picks where to stop and how to get there. Ships are
        /// huge (a fighter is about 29 m long, a bomber 45 m), so this keeps them from flying into each other.
        /// </summary>
        public const float ShipSeparation = 45f;

        // ---------- Raids on the city ----------
        /// <summary>
        /// Raids each ship makes on the city before it turns on the tower: 3 in the first three waves, 2 in the next
        /// three, then 1. Meanwhile the tower is safe, so a player who brings the ships down over the city keeps the
        /// tower whole, and the match has time to grow harder before the tower is in danger. In simulated matches with
        /// the real rules, a player who never shoots lasts about 4 minutes of fight, a weak player 5, an average one 6
        /// and an expert 9 (plus the ride up).
        /// </summary>
        public const int FirstWaveCityRuns = 3;
        public const int WavesPerCityRunLost = 3;
        public const int MinCityRuns = 1;

        /// <summary>
        /// Most ships attacking the tower at the same time: 1 in the first two waves, one more every two waves, up to
        /// 4. A ship that finishes its raids while the tower is taken keeps raiding the city until there is room, so
        /// the damage to the tower grows wave after wave instead of every ship of a big wave turning on it at once.
        /// </summary>
        public const int FirstWaveTowerAttackers = 1;
        public const int MaxTowerAttackers = 4;

        /// <summary>Shots a ship fires down at the city on each raid, and the seconds between them.</summary>
        public const int CityShotsPerRun = 3;
        public const float CityShotInterval = 0.7f;

        /// <summary>
        /// Where ships stop over the city (meters, relative to the player platform): farther out than the tower
        /// stations and below the player, but well above the roofs (the tallest is 100 m under the top).
        /// </summary>
        public const float MinRaidRadius = 90f;
        public const float MaxRaidRadius = 180f;
        public const float MinRaidHeight = -70f;
        public const float MaxRaidHeight = -30f;

        // ---------- Ships ----------
        /// <summary>
        /// Speed (m/s) at which a ship shoots out of its portal. It then brakes hard (LaunchBraking, m/s²) down to its
        /// cruise speed, so a new ship bursts into the sky instead of drifting out.
        /// </summary>
        public const float FighterLaunchSpeed = 60f;
        public const float BomberLaunchSpeed = 36f;
        public const float LaunchBraking = 35f;

        /// <summary>Bolts a fighter takes (half a second of burst).</summary>
        public const int FighterHealth = 5;
        public const float FighterSpeed = 16f;
        public const float FighterAimTime = 1.5f;
        public const float FighterDamage = 3f;
        /// <summary>How fast a fighter speeds up and slows down (m/s²): agile, but never swaying.</summary>
        public const float FighterAcceleration = 9f;
        /// <summary>Chance that a fighter splits its way to the next station into two straight dashes.</summary>
        public const float FighterDoglegChance = 0.6f;
        /// <summary>How far sideways (meters) the turning point of a fighter's two dashes may be.</summary>
        public const float FighterDoglegOffset = 18f;

        /// <summary>Bolts a bomber takes (a bit over a second of burst).</summary>
        public const int BomberHealth = 12;
        public const float BomberSpeed = 8f;
        public const float BomberAimTime = 2.5f;
        public const float BomberDamage = 9f;
        /// <summary>A bomber is heavy: it speeds up and slows down slowly (m/s²).</summary>
        public const float BomberAcceleration = 2.5f;

        /// <summary>
        /// Flight zone around the tower (meters, relative to the player platform). The ships are huge (a fighter is
        /// about 26 m across, a bomber about 48 m long), so they keep their distance to fit in view.
        /// </summary>
        public const float MinFlightRadius = 45f;
        /// <summary>No straight dash passes closer than this to the tower's axis (meters): ships fly around the tower.</summary>
        public const float FlightClearance = 35f;
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
