using System;
using System.Numerics;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Everything the game needs to know to spawn one wave. Value type (struct) because it is
    /// small, immutable in practice and copied around freely between systems.
    /// </summary>
    public struct WaveConfig
    {
        public int Number;
        public int Portals;

        /// <summary>Total ships in this wave, shared between its portals.</summary>
        public int TotalShips;

        /// <summary>Most ships of this wave flying at the same time; the others wait in their portals.</summary>
        public int MaxShipsInAir;

        /// <summary>Raids on the city each ship makes before it turns on the tower.</summary>
        public int CityRuns;
        public float SpeedMultiplier;
        public float AimTimeFighter;
        public float AimTimeBomber;
        public float BomberRatio;
        public int Seed;
    }

    /// <summary>
    /// Where a portal opens in the sky, expressed in spherical coordinates around the tower.
    /// Azimuth 0 = North, 90 = East (clockwise, like a compass).
    /// </summary>
    public struct PortalPlacement
    {
        public float AzimuthDegrees;
        public float ElevationDegrees;
        public float Distance;

        /// <summary>Position in tower space (X = East, Y = Up, Z = North), measured from the player's platform.</summary>
        public Vector3 ToPosition()
        {
            double azimuth = AzimuthDegrees * Math.PI / 180.0;
            double elevation = ElevationDegrees * Math.PI / 180.0;
            float horizontal = (float)(Distance * Math.Cos(elevation));
            return new Vector3(
                (float)Math.Sin(azimuth) * horizontal,
                (float)(Distance * Math.Sin(elevation)),
                (float)Math.Cos(azimuth) * horizontal);
        }
    }

    /// <summary>
    /// One frame of "how the tower is moving". It is the only thing motion outputs receive,
    /// so a future actuator platform can reproduce the game without knowing any game rules.
    /// Units: degrees for Pitch/Roll, meters for Heave (negative = down), 0..1 for Shake, seconds for Time.
    /// </summary>
    public struct MotionSample
    {
        public float Pitch;  // positive = leaning towards North
        public float Roll;   // positive = leaning towards East
        public float Heave;  // accumulated vertical drop in meters (negative)
        public float Shake;  // vibration intensity 0..1
        public float Time;   // seconds since the defense started

        public MotionSample(float pitch, float roll, float heave, float shake, float time)
        {
            Pitch = pitch;
            Roll = roll;
            Heave = heave;
            Shake = shake;
            Time = time;
        }

        /// <summary>A sample with no motion at all (used to reset outputs).</summary>
        public static MotionSample Level => new MotionSample(0f, 0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Final result of a match. Marked [Serializable] (a System attribute, not Unity) so it
    /// can be written to JSON by the serializer chosen in the Unity layer.
    /// Public fields (not properties) keep it compatible with UnityEngine.JsonUtility.
    /// </summary>
    [Serializable]
    public struct MatchResult
    {
        public string Initials;
        public float SecondsSurvived;
        public int ShipsDestroyed;
        public int ShotsFired;
        public int ShotsHit;
        public int WaveReached;
        public int Score;
        public string DateIso;

        /// <summary>Hit ratio between 0 and 1 (0 when the player never fired).</summary>
        public float Accuracy => ShotsFired == 0 ? 0f : (float)ShotsHit / ShotsFired;
    }
}
