namespace AlienInvaders.Core
{
    /// <summary>
    /// High-level phases of one play session. The GameController (Unity layer) switches
    /// between these states; every transition is driven by events from the core systems.
    /// </summary>
    public enum GameState
    {
        Calibration, // AR: player stands on the physical platform and sets the origin
        Ascent,      // AR -> VR: closed elevator rises to the top of the tower
        Arrival,     // VR: elevator opens, weapon appears
        Defense,     // VR: ships are attacking
        Respite,     // VR: short calm + portals opening before the next wave
        Critical,    // VR: integrity <= 25 %, alarms, constant shaking (still playable)
        Rescue,      // VR: integrity = 0, emergency elevator drops the player
        Results      // VR -> AR: score panels in the real room
    }

    /// <summary>
    /// The four facades of the tower. The integer values are used as array indexes,
    /// so they must stay 0..3.
    /// </summary>
    public enum TowerSide
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3
    }

    /// <summary>Kinds of enemy ships (Mothership is reserved for a future wave type).</summary>
    public enum ShipType
    {
        Fighter,
        Bomber,
        Mothership
    }

    /// <summary>
    /// Life cycle of a ship: it shoots out of the portal, raids the city a few times and only then turns on the tower.
    /// Aiming exists so the player is always warned before the tower takes damage (design pillar: fairness).
    /// </summary>
    public enum ShipPhase
    {
        Emerging,     // shooting out of the portal, braking down to its cruise speed
        Raiding,      // flying to a point over the city
        Bombing,      // stopped over the city, firing down at the buildings (the tower is safe)
        Approaching,  // flying to its firing station near the tower
        Aiming,       // red aiming laser visible: destroying the ship now prevents the damage
        Destroyed
    }

    /// <summary>Sub-phases of the wave cycle (calm -> portals opening -> attack).</summary>
    public enum WavePhase
    {
        Peace,
        PortalsOpening,
        Attacking
    }
}
