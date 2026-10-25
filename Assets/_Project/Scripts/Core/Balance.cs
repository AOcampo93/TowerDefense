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
        /// <summary>Portals of the first wave (two, so the invasion comes from two sides at once); one more every two waves.</summary>
        public const int FirstWavePortals = 2;
        public const int MaxPortals = 4;
        public const float MaxSpeedMultiplier = 2f;
        public const float SpeedIncreasePerWave = 0.05f;

        /// <summary>
        /// Fighters get faster every wave much more than bombers (10 % against 5 %, up to 2.2 times their first
        /// speed), so they get harder to aim at as the match goes on while the bombers stay slow and heavy.
        /// </summary>
        public const float FighterSpeedIncreasePerWave = 0.1f;
        public const float MaxFighterSpeedMultiplier = 2.2f;
        public const float AimTimeDecreasePerWave = 0.08f;
        public const float MinAimTime = 0.6f;
        public const float BomberRatioPerWave = 0.05f;
        public const float MaxBomberRatio = 0.4f;

        /// <summary>
        /// Calm after a cleared wave before the next portals open (seconds). With the portals opening that is only a
        /// few seconds of rest: the fight is constant and the player never waits for the next wave.
        /// </summary>
        public const float PeaceSeconds = 2f;

        /// <summary>
        /// Longest attack (seconds): when the sky is not cleared in time, the next wave opens its portals anyway and the
        /// ships still flying stay. The difficulty keeps growing with time even for a player who cannot keep up.
        /// </summary>
        public const float MaxWaveSeconds = 30f;
        public const float PortalOpeningSeconds = 4f;

        /// <summary>
        /// Seconds between two ships coming out of the same portal: they pour out one after another, like an invasion.
        /// </summary>
        public const float SpawnIntervalSeconds = 0.8f;

        /// <summary>
        /// How far (meters) the last ship must have flown from a portal before the next one comes out of it, so two
        /// hulls never come out on top of each other.
        /// </summary>
        public const float PortalClearance = 30f;

        /// <summary>Ships in the first wave; every wave brings this many more.</summary>
        public const int FirstWaveShips = 6;
        public const int ShipsAddedPerWave = 2;

        /// <summary>
        /// Most ships flying at the same time: 5 in the first wave, one more every wave, up to 10. A full sky from the
        /// start, so the player does not know where to begin; the rest wait in their portals.
        /// </summary>
        public const int FirstWaveShipsInAir = 5;
        public const int MaxShipsInAir = 10;

        /// <summary>
        /// Distance (meters) a ship keeps from the others when it picks where to stop and how to get there. Ships are
        /// huge (a fighter is about 29 m long, a bomber 45 m), so this keeps them from flying into each other.
        /// </summary>
        public const float ShipSeparation = 45f;

        // ---------- Raids on the city ----------
        /// <summary>
        /// Raids each ship makes on the city before it turns on the tower: 3 in the first four waves, 2 in the next
        /// four, then 1. Meanwhile the tower is safe, so a player who brings the ships down over the city keeps the
        /// tower whole, and the match has time to grow harder before the tower is in danger. In simulated matches with
        /// the real rules (fast waves, power ups), a player who never shoots lasts about 4 minutes of fight, a weak
        /// player 4.4, an average one 5.2 and an expert 7.2 (plus the ride up).
        /// </summary>
        public const int FirstWaveCityRuns = 3;
        public const int WavesPerCityRunLost = 4;
        public const int MinCityRuns = 1;

        /// <summary>
        /// Most ships attacking the tower at the same time: 1 in the first three waves, one more every three waves, up
        /// to 4. A ship that finishes its raids while the tower is taken keeps raiding the city until there is room, so
        /// the damage to the tower grows wave after wave instead of every ship of a big wave turning on it at once.
        /// </summary>
        public const int FirstWaveTowerAttackers = 1;
        public const int WavesPerTowerAttacker = 3;
        public const int MaxTowerAttackers = 4;

        /// <summary>Shots a fighter fires down at the city on each raid, and the seconds between them.</summary>
        public const int CityShotsPerRun = 3;
        public const float CityShotInterval = 0.7f;

        /// <summary>
        /// A bomber drops one heavy bomb per raid instead of a few shots: a huge blast on the city that the player
        /// hears and feels on the platform (BombJolt), even far away.
        /// </summary>
        public const int BomberBombsPerRun = 1;

        /// <summary>Shake (0 to 1) of the platform when a bomb lands on the city: a jolt, lighter than a hit on the tower.</summary>
        public const float BombJolt = 0.45f;

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
        public const float BomberLaunchSpeed = 45f;
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

        // ---------- Power ups ----------
        /// <summary>
        /// Power ups come only from the big ships: from this wave on (or earlier, whenever the sky holds PowerUpCrowd
        /// ships or more), every BombersPerPowerUp-th bomber brought down leaves glowing cubes where it blew up. They
        /// come out of the blast PowerUpAppearSeconds later, so the stream of bolts that brought the ship down does not
        /// grab them unseen, and take PowerUpHits bolts to collect (about half a second of fire). They stay only
        /// PowerUpFloatSeconds; collecting them makes every bolt hit BoostDamageMultiplier times harder for
        /// BoostSeconds (and cools the weapon), the help the player needs when the waves grow bigger than their aim.
        /// </summary>
        public const int PowerUpFirstWave = 5;
        public const int PowerUpCrowd = 8;
        public const int BombersPerPowerUp = 2;
        public const float PowerUpAppearSeconds = 1f;
        public const int PowerUpHits = 6;
        public const float PowerUpFloatSeconds = 6f;
        public const float BoostSeconds = 8f;
        public const int BoostDamageMultiplier = 3;

        // ---------- Score ----------
        public const int PointsPerSecond = 10;
        public const int PointsPerKill = 5;
        public const int AccuracyBonus = 1000;
        public const int HighScoreEntries = 10;
    }
}
