using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Base class of every alien ship (stretch challenge: inheritance with an abstract class).
    /// It owns the shared life cycle: leave the portal, fly to a firing station, aim (warning
    /// laser) and fire at the tower. Subclasses only decide HOW they get to a station and their stats.
    /// Ships fly like alien craft: in straight dashes that speed up and slow down smoothly, never
    /// swaying, and they come to rest exactly on their station. A dash never passes through the
    /// tower: when the straight way would, the ship turns at a point beside it. With an Airspace, each ship
    /// picks a station and a way that keep it away from the other ships.
    ///
    /// Coordinates: tower center at the origin, X = East, Y = Up, Z = North (same as Unity).
    /// </summary>
    public abstract class EnemyShip
    {
        private const float EmergeSeconds = 1f;
        private const float ArrivalDistance = 2f;
        // Possible stations tried when the sky is shared with other ships.
        private const int StationCandidates = 16;

        protected readonly Random Random;
        private readonly Airspace _airspace;
        private float _phaseTimer;
        private float _currentSpeed;
        // The way to the current destination, with its optional turning point.
        private Vector3 _wayEnd;
        private Vector3 _turningPoint;
        private bool _hasTurningPoint;

        public ShipType Type { get; protected set; }
        public int Health { get; protected set; }

        /// <summary>Health when the ship came out of the portal (for its health bar).</summary>
        public int MaxHealth { get; protected set; }
        public float Speed { get; protected set; }

        /// <summary>How fast the ship speeds up and slows down (m/s²).</summary>
        public float Acceleration { get; protected set; }
        public float DamageToTower { get; protected set; }
        public float AimTime { get; protected set; }
        public ShipPhase Phase { get; private set; } = ShipPhase.Emerging;

        /// <summary>Current position in world space (meters).</summary>
        public Vector3 Position { get; protected set; }

        /// <summary>Point in the sky where the ship stops to aim and fire.</summary>
        public Vector3 Station { get; private set; }

        /// <summary>Where the ship turns on its way to the station (the station itself when it flies straight there).</summary>
        public Vector3 TurningPoint => _hasTurningPoint ? _turningPoint : Station;

        /// <summary>Point on the tower facade that the ship is targeting.</summary>
        public Vector3 TargetPoint { get; private set; }

        /// <summary>Facade that will receive the damage.</summary>
        public TowerSide TargetSide { get; private set; }

        /// <summary>Seconds left before the shot (only meaningful while Aiming).</summary>
        public float AimTimeLeft => Phase == ShipPhase.Aiming ? _phaseTimer : 0f;

        public bool IsAlive => Phase != ShipPhase.Destroyed;

        /// <summary>Raised when the warning laser appears (Unity: red thin laser + charging sound).</summary>
        public event Action<EnemyShip> OnAimStarted;

        /// <summary>Raised when the ship hits the tower: (ship, side, damage).</summary>
        public event Action<EnemyShip, TowerSide, float> OnFire;

        /// <summary>Raised once when health reaches zero.</summary>
        public event Action<EnemyShip> OnDestroyed;

        /// <param name="spawnPosition">Portal position.</param>
        /// <param name="random">Shared deterministic random generator (wave seed).</param>
        /// <param name="airspace">The other ships, to keep away from them (null: the ship flies alone).</param>
        protected EnemyShip(Vector3 spawnPosition, Random random, Airspace airspace)
        {
            Position = spawnPosition;
            Random = random;
            _airspace = airspace;
            _phaseTimer = EmergeSeconds;
            ChooseNewStation();
        }

        /// <summary>
        /// How each ship type gets to a new destination (polymorphism): straight, or in two dashes that stop at a
        /// turning point. Returns false to fly straight there.
        /// </summary>
        protected abstract bool PickTurningPoint(Vector3 from, Vector3 to, out Vector3 turningPoint);

        /// <summary>Advances the shared life cycle. Called every frame by the Unity layer.</summary>
        public virtual void Tick(float deltaTime)
        {
            switch (Phase)
            {
                case ShipPhase.Emerging:
                    // Drift slowly out of the portal towards the station.
                    Position = FlyTowards(Station, Speed * 0.3f, deltaTime);
                    _phaseTimer -= deltaTime;
                    if (_phaseTimer <= 0f)
                    {
                        Phase = ShipPhase.Approaching;
                    }
                    break;

                case ShipPhase.Approaching:
                    Position = FlyTowards(Station, Speed, deltaTime);
                    if (Vector3.Distance(Position, Station) <= ArrivalDistance)
                    {
                        Phase = ShipPhase.Aiming;
                        _phaseTimer = AimTime;
                        OnAimStarted?.Invoke(this);
                    }
                    break;

                case ShipPhase.Aiming:
                    // The last meters to the station, slowing down to a stop while the warning laser grows.
                    Position = FlyTowards(Station, Speed, deltaTime);
                    _phaseTimer -= deltaTime;
                    if (_phaseTimer <= 0f)
                    {
                        // Warning finished and the ship survived: the tower takes the hit.
                        OnFire?.Invoke(this, TargetSide, DamageToTower);
                        ChooseNewStation();
                        Phase = ShipPhase.Approaching;
                    }
                    break;

                case ShipPhase.Destroyed:
                    break;
            }
        }

        /// <summary>
        /// Applies weapon damage. Destroying the ship while it is aiming cancels its shot,
        /// which is the core skill of the game.
        /// </summary>
        public virtual void Hit(int damage)
        {
            if (!IsAlive || damage <= 0)
            {
                return;
            }

            Health -= damage;
            if (Health <= 0)
            {
                Health = 0;
                Phase = ShipPhase.Destroyed;
                OnDestroyed?.Invoke(this);
            }
        }

        /// <summary>
        /// Picks a random point in the flight zone and the facade point it will shoot at. Ships aim a few meters
        /// below their own height, but always somewhere on the facade under the player. In a shared sky a few random
        /// points are tried: the first one whose way stays far enough from the other ships wins, or the freest one
        /// when the sky is crowded.
        /// </summary>
        protected void ChooseNewStation()
        {
            int tries = _airspace == null ? 1 : StationCandidates;
            float azimuth = 0f;
            float height = 0f;
            float bestClearance = -1f;
            for (int i = 0; i < tries && bestClearance < Balance.ShipSeparation; i++)
            {
                float candidateAzimuth = (float)(Random.NextDouble() * 360.0);
                float radius = Lerp(Balance.MinFlightRadius, Balance.MaxFlightRadius, (float)Random.NextDouble());
                float candidateHeight = Lerp(Balance.MinFlightHeight, Balance.MaxFlightHeight, (float)Random.NextDouble());
                Vector3 candidate = FromAzimuth(candidateAzimuth, radius, candidateHeight);
                // The whole way counts, turning point included (subclasses only read Random and Balance here, so
                // this is safe even while the base constructor runs).
                bool turns = PickTurningPoint(Position, candidate, out Vector3 turn);
                Vector3 via = turns ? turn : candidate;
                float clearance = _airspace == null ? 0f : _airspace.Clearance(this, Position, via, candidate);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    Station = candidate;
                    azimuth = candidateAzimuth;
                    height = candidateHeight;
                    _wayEnd = candidate;
                    _hasTurningPoint = turns;
                    _turningPoint = turn;
                }
            }

            TargetSide = TowerIntegrity.SideFromAzimuth(azimuth);
            float impactHeight = Clamp(height - 8f, Balance.MinImpactHeight, Balance.MaxImpactHeight);
            TargetPoint = FacadePoint(TargetSide, azimuth, impactHeight);
        }

        /// <summary>Converts compass azimuth + horizontal distance + height into a position.</summary>
        public static Vector3 FromAzimuth(float azimuthDegrees, float distance, float height)
        {
            double rad = azimuthDegrees * Math.PI / 180.0;
            return new Vector3((float)Math.Sin(rad) * distance, height, (float)Math.Cos(rad) * distance);
        }

        /// <summary>Compass azimuth (0 = North, 90 = East) of a position seen from the tower.</summary>
        public static float AzimuthOf(Vector3 position)
        {
            double degrees = Math.Atan2(position.X, position.Z) * 180.0 / Math.PI;
            return (float)((degrees + 360.0) % 360.0);
        }

        /// <summary>Point on the given facade, aligned with the azimuth, clamped to the tower width.</summary>
        private static Vector3 FacadePoint(TowerSide side, float azimuthDegrees, float height)
        {
            Vector3 dir = FromAzimuth(azimuthDegrees, 1f, 0f);
            float w = Balance.TowerHalfWidth;
            switch (side)
            {
                case TowerSide.North: return new Vector3(Clamp(dir.X * w * 2f, -w, w), height, w);
                case TowerSide.South: return new Vector3(Clamp(dir.X * w * 2f, -w, w), height, -w);
                case TowerSide.East: return new Vector3(w, height, Clamp(dir.Z * w * 2f, -w, w));
                default: return new Vector3(-w, height, Clamp(dir.Z * w * 2f, -w, w));
            }
        }

        // Next position on the way to the destination: through the turning point first, if the way has one.
        private Vector3 FlyTowards(Vector3 destination, float maxSpeed, float deltaTime)
        {
            if (destination != _wayEnd)
            {
                _wayEnd = destination;
                _hasTurningPoint = PickTurningPoint(Position, destination, out _turningPoint);
            }
            if (_hasTurningPoint)
            {
                Vector3 next = Glide(Position, _turningPoint, maxSpeed, deltaTime);
                _hasTurningPoint = Vector3.Distance(next, _turningPoint) > 0.05f;
                return next;
            }
            return Glide(Position, destination, maxSpeed, deltaTime);
        }

        // One straight dash: the ship speeds up gently up to maxSpeed and slows down in time to stop exactly on the
        // destination (the fastest speed that can still stop in the distance left is sqrt(2 · a · d)).
        private Vector3 Glide(Vector3 current, Vector3 destination, float maxSpeed, float deltaTime)
        {
            float distance = Vector3.Distance(current, destination);
            float canStop = (float)Math.Sqrt(2f * Acceleration * distance);
            _currentSpeed = Math.Min(_currentSpeed + Acceleration * deltaTime, Math.Min(maxSpeed, canStop));
            return MoveTowards(current, destination, _currentSpeed * deltaTime);
        }

        /// <summary>
        /// When the straight way from one point to another would pass too close to the tower (seen from above), gives
        /// a turning point beside the tower at the edge of the flight zone, so the ship goes around it in two dashes.
        /// </summary>
        protected static bool AroundTheTower(Vector3 from, Vector3 to, out Vector3 turningPoint)
        {
            var start = new Vector2(from.X, from.Z);
            Vector2 way = new Vector2(to.X, to.Z) - start;
            float t = way.LengthSquared() > 0.01f ? Clamp(-Vector2.Dot(start, way) / way.LengthSquared(), 0f, 1f) : 0f;
            Vector2 closest = start + way * t;
            if (closest.Length() >= Balance.FlightClearance)
            {
                turningPoint = to;
                return false;
            }
            // Out from the tower on the side the way already leans to (straight through: either side).
            Vector2 outward = closest.Length() > 0.1f ? Vector2.Normalize(closest) : Vector2.Normalize(new Vector2(way.Y, -way.X));
            float height = Lerp(from.Y, to.Y, t);
            turningPoint = new Vector3(outward.X * Balance.MinFlightRadius, height, outward.Y * Balance.MinFlightRadius);
            return true;
        }

        /// <summary>Moves a point towards a target by at most maxDistance.</summary>
        protected static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistance)
        {
            Vector3 delta = target - current;
            float distance = delta.Length();
            if (distance <= maxDistance || distance < 0.0001f)
            {
                return target;
            }
            return current + delta / distance * maxDistance;
        }

        protected static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
