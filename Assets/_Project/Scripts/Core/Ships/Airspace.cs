using System;
using System.Collections.Generic;
using System.Numerics;

namespace AlienInvaders.Core.Ships
{
    /// <summary>
    /// The sky shared by the ships of a match. Before a ship picks where to stop next, it asks how free each possible
    /// way is, so ships keep their distance (Balance.ShipSeparation) instead of stopping inside each other or flying
    /// through each other. Portals also ask it before releasing a ship, so a new ship never comes out on top of the
    /// last one. It only knows where the ships are and where they are going; it never moves them.
    /// </summary>
    public class Airspace
    {
        // Points measured along a way (its start is left out: it is where the ship already is).
        private const int Samples = 6;

        private readonly List<EnemyShip> _ships = new List<EnemyShip>();

        /// <summary>Ships currently registered.</summary>
        public int Count => _ships.Count;

        public void Add(EnemyShip ship)
        {
            if (!_ships.Contains(ship))
            {
                _ships.Add(ship);
            }
        }

        public void Remove(EnemyShip ship)
        {
            _ships.Remove(ship);
        }

        public void Clear()
        {
            _ships.Clear();
        }

        /// <summary>
        /// How free a way is for a ship: the shortest distance from the way (from its position through a turning point
        /// to a possible station) to the way every other ship is flying now, which ends at the station where that
        /// ship will stop.
        /// </summary>
        public float Clearance(EnemyShip self, Vector3 from, Vector3 via, Vector3 station)
        {
            float clearance = float.MaxValue;
            foreach (EnemyShip other in _ships)
            {
                if (other == self || !other.IsAlive)
                {
                    continue;
                }
                for (int i = 1; i <= Samples; i++)
                {
                    float t = (float)i / Samples;
                    clearance = Math.Min(clearance, DistanceToWay(Vector3.Lerp(from, via, t), other));
                    clearance = Math.Min(clearance, DistanceToWay(Vector3.Lerp(via, station, t), other));
                }
            }
            return clearance;
        }

        /// <summary>True when no ship is closer than the given distance to a point (a portal ready to release a ship).</summary>
        public bool IsClear(Vector3 point, float distance)
        {
            foreach (EnemyShip other in _ships)
            {
                if (other.IsAlive && Vector3.Distance(point, other.Position) < distance)
                {
                    return false;
                }
            }
            return true;
        }

        // Distance from a point to the way a ship is flying: to its turning point, then to its station.
        private static float DistanceToWay(Vector3 point, EnemyShip ship)
        {
            return Math.Min(DistanceToSegment(point, ship.Position, ship.TurningPoint),
                DistanceToSegment(point, ship.TurningPoint, ship.Station));
        }

        // Distance from a point to the closest point of a segment.
        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            float t = lengthSquared > 0.0001f ? Vector3.Dot(point - start, segment) / lengthSquared : 0f;
            t = Math.Max(0f, Math.Min(1f, t));
            return Vector3.Distance(point, start + segment * t);
        }
    }
}
