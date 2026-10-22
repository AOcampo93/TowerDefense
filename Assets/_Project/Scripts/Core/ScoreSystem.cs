using System;
using System.Globalization;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Tracks the statistics of a match and turns them into a score.
    /// Time survived is the main metric; kills and accuracy break ties between similar runs.
    /// </summary>
    public class ScoreSystem
    {
        public float SecondsSurvived { get; private set; }
        public int ShipsDestroyed { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }

        /// <summary>Hit ratio between 0 and 1.</summary>
        public float Accuracy => ShotsFired == 0 ? 0f : (float)ShotsHit / ShotsFired;

        /// <summary>Current score, recalculated on demand.</summary>
        public int CurrentScore => Calculate(SecondsSurvived, ShipsDestroyed, Accuracy);

        /// <summary>Adds survived time; called every frame while defending.</summary>
        public void Tick(float deltaTime)
        {
            SecondsSurvived += deltaTime;
        }

        /// <summary>Counts one shot and whether it hit a ship.</summary>
        public void RegisterShot(bool hit)
        {
            ShotsFired++;
            if (hit)
            {
                ShotsHit++;
            }
        }

        /// <summary>Counts one destroyed ship (the type is kept for future per-type bonuses).</summary>
        public void RegisterKill(ShipType type)
        {
            ShipsDestroyed++;
        }

        /// <summary>
        /// score = seconds * 10 + kills * 5 + accuracy * 1000.
        /// Static and pure so it can be unit tested and explained easily.
        /// </summary>
        public static int Calculate(float seconds, int kills, float accuracy)
        {
            float clampedAccuracy = Math.Max(0f, Math.Min(1f, accuracy));
            return (int)Math.Floor(seconds) * Balance.PointsPerSecond
                   + kills * Balance.PointsPerKill
                   + (int)Math.Round(clampedAccuracy * Balance.AccuracyBonus);
        }

        /// <summary>Builds the final result that is shown and saved to the high-score file.</summary>
        public MatchResult BuildResult(string initials, int waveReached, DateTime utcNow)
        {
            return new MatchResult
            {
                Initials = string.IsNullOrEmpty(initials) ? "???" : initials.ToUpperInvariant(),
                SecondsSurvived = SecondsSurvived,
                ShipsDestroyed = ShipsDestroyed,
                ShotsFired = ShotsFired,
                ShotsHit = ShotsHit,
                WaveReached = waveReached,
                Score = CurrentScore,
                DateIso = utcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }

        /// <summary>Clears all statistics for a new match.</summary>
        public void Reset()
        {
            SecondsSurvived = 0f;
            ShipsDestroyed = 0;
            ShotsFired = 0;
            ShotsHit = 0;
        }
    }
}
