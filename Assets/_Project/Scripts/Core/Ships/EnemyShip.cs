using System;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// Base class of every alien ship (stretch challenge: inheritance with an abstract class).
    /// It owns the shared life cycle: leave the portal, fly to a firing station, aim (warning
    /// laser) and fire at the tower. Subclasses only decide HOW they fly and their stats.
    ///
    /// Coordinates: tower center at the origin, X = East, Y = Up, Z = North (same as Unity).
    /// </summary>
    public abstract class EnemyShip
    {
        private const float EmergeSeconds = 1f;
        private const float ArrivalDistance = 0.75f;

        protected readonly Random Random;
        private float _phaseTimer;

        public ShipType Type { get; protected set; }
        public int Health { get; protected set; }
        public float Speed { get; protected set; }
        public float DamageToTower { get; protected set; }
        public float AimTime { get; protected set; }
        public ShipPhase Phase { get; private set; } = ShipPhase.Emerging;

        /// <summary>Current position in world space (meters).</summary>
        public Vector3 Position { get; protected set; }

        /// <summary>Point in the sky where the ship stops to aim and fire.</summary>
        public Vector3 Station { get; private set; }

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
        protected EnemyShip(Vector3 spawnPosition, Random random)
        {
            Position = spawnPosition;
            Random = random;
            _phaseTimer = EmergeSeconds;
            ChooseNewStation();
        }

        /// <summary>
        /// Movement that is specific to each ship type (polymorphism): returns the next
        /// position on the way to the station.
        /// </summary>
        protected abstract Vector3 ComputeNextPosition(Vector3 current, Vector3 destination, float deltaTime);

        /// <summary>Advances the shared life cycle. Called every frame by the Unity layer.</summary>
        public virtual void Tick(float deltaTime)
        {
            switch (Phase)
            {
                case ShipPhase.Emerging:
                    // Drift slowly out of the portal towards the station.
                    Position = MoveTowards(Position, Station, Speed * 0.3f * deltaTime);
                    _phaseTimer -= deltaTime;
                    if (_phaseTimer <= 0f)
                    {
                        Phase = ShipPhase.Approaching;
                    }
                    break;

                case ShipPhase.Approaching:
                    Position = ComputeNextPosition(Position, Station, deltaTime);
                    if (Vector3.Distance(Position, Station) <= ArrivalDistance)
                    {
                        Phase = ShipPhase.Aiming;
                        _phaseTimer = AimTime;
                        OnAimStarted?.Invoke(this);
                    }
                    break;

                case ShipPhase.Aiming:
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
        /// below their own height, but always somewhere on the facade under the player.
        /// </summary>
        protected void ChooseNewStation()
        {
            float azimuth = (float)(Random.NextDouble() * 360.0);
            float radius = Lerp(Balance.MinFlightRadius, Balance.MaxFlightRadius, (float)Random.NextDouble());
            float height = Lerp(Balance.MinFlightHeight, Balance.MaxFlightHeight, (float)Random.NextDouble());

            Station = FromAzimuth(azimuth, radius, height);
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
