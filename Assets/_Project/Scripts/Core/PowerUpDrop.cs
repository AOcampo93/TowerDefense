using System;

namespace AlienInvaders.Core
{
    /// <summary>
    /// One power up, from the moment its ship blows up until it is collected or vanishes. The cubes come out of the
    /// blast a moment later (Balance.PowerUpAppearSeconds), so the bolts that brought the ship down do not grab them
    /// before the player even sees them, and they take Balance.PowerUpHits bolts to collect: they light up with every
    /// hit, so the player sees them and knows they took them. Not collected within Balance.PowerUpFloatSeconds, they
    /// vanish. The Unity layer shows the cubes; the rules of when they can be shot live here.
    /// </summary>
    public class PowerUpDrop
    {
        private float _age;

        /// <summary>Bolts that hit the cubes so far.</summary>
        public int Hits { get; private set; }

        /// <summary>True once enough bolts hit the cubes.</summary>
        public bool Collected { get; private set; }

        /// <summary>Collected, or not shot in time.</summary>
        public bool Gone => Collected || _age >= Balance.PowerUpAppearSeconds + Balance.PowerUpFloatSeconds;

        /// <summary>True while the cubes float in the sky and can be shot (not during the blast, not once gone).</summary>
        public bool Visible => !Gone && _age >= Balance.PowerUpAppearSeconds;

        /// <summary>Seconds since the cubes came out of the blast (0 before).</summary>
        public float ShownSeconds => Math.Max(0f, _age - Balance.PowerUpAppearSeconds);

        /// <summary>Seconds before the cubes vanish.</summary>
        public float TimeLeft => Math.Max(0f, Balance.PowerUpAppearSeconds + Balance.PowerUpFloatSeconds - _age);

        /// <summary>How close the cubes are to being collected: 0 untouched, 1 collected.</summary>
        public float Charge => (float)Hits / Balance.PowerUpHits;

        /// <summary>Counts the time since the blast.</summary>
        public void Tick(float deltaTime)
        {
            _age += deltaTime;
        }

        /// <summary>
        /// A bolt hit the cubes. Returns true when this bolt collects them. Hits while the cubes are still inside the
        /// blast or already gone do nothing.
        /// </summary>
        public bool Hit()
        {
            if (!Visible)
            {
                return false;
            }
            Hits++;
            if (Hits < Balance.PowerUpHits)
            {
                return false;
            }
            Collected = true;
            return true;
        }

        /// <summary>A new power up, for a new blast.</summary>
        public void Reset()
        {
            _age = 0f;
            Hits = 0;
            Collected = false;
        }
    }
}
