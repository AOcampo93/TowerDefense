using System;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Power ups for crowded skies. Once the waves grow big (from Balance.PowerUpFirstWave, or earlier whenever the sky
    /// is full), some of the big ships leave glowing cubes where they blew up: every Balance.BombersPerPowerUp-th
    /// bomber brought down. Shooting them starts a boost: for a few seconds every bolt hits several times harder, so a
    /// player overwhelmed by the number of ships can clear the sky again. The Unity layer shows the cubes and the
    /// stronger beam; the rules of when they drop and how long the boost lasts live here.
    /// </summary>
    public class WeaponBoost
    {
        private int _bombers;

        /// <summary>Seconds of boost left (0 when the weapon is normal).</summary>
        public float TimeLeft { get; private set; }

        /// <summary>True while the boost lasts.</summary>
        public bool Active => TimeLeft > 0f;

        /// <summary>Damage of one bolt right now.</summary>
        public int ShotDamage => Active ? Balance.ShotDamage * Balance.BoostDamageMultiplier : Balance.ShotDamage;

        /// <summary>Raised when a boost starts (not when a new power up only makes it last longer).</summary>
        public event Action OnStarted;

        /// <summary>Raised on every power up collected, also when it only makes the boost last longer.</summary>
        public event Action OnCollected;

        /// <summary>Raised when the boost runs out.</summary>
        public event Action OnEnded;

        /// <summary>
        /// Counts a ship brought down. Returns true when this kill leaves a power up: only bombers brought down while
        /// power ups are allowed count, and every Balance.BombersPerPowerUp-th of them drops one.
        /// </summary>
        /// <param name="type">Type of the ship brought down.</param>
        /// <param name="waveNumber">Wave being fought.</param>
        /// <param name="shipsInAir">Ships still flying after this kill.</param>
        public bool RegisterKill(ShipType type, int waveNumber, int shipsInAir)
        {
            if (type != ShipType.Bomber || (waveNumber < Balance.PowerUpFirstWave && shipsInAir < Balance.PowerUpCrowd))
            {
                return false;
            }
            _bombers++;
            if (_bombers < Balance.BombersPerPowerUp)
            {
                return false;
            }
            _bombers = 0;
            return true;
        }

        /// <summary>The player shot a power up: the boost starts, or starts over if it was already on.</summary>
        public void Collect()
        {
            bool wasActive = Active;
            TimeLeft = Balance.BoostSeconds;
            if (!wasActive)
            {
                OnStarted?.Invoke();
            }
            OnCollected?.Invoke();
        }

        /// <summary>Counts the boost down.</summary>
        public void Tick(float deltaTime)
        {
            if (!Active)
            {
                return;
            }
            TimeLeft -= deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                OnEnded?.Invoke();
            }
        }

        /// <summary>No boost and no kills counted, for a new match.</summary>
        public void Reset()
        {
            bool wasActive = Active;
            _bombers = 0;
            TimeLeft = 0f;
            if (wasActive)
            {
                OnEnded?.Invoke();
            }
        }
    }
}
