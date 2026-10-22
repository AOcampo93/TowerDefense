using System;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Overheating model of the player's weapon. There is no ammo: firing heats the barrel and
    /// holding the trigger for too long locks the weapon for a moment. This rewards aiming
    /// instead of spraying.
    /// </summary>
    public class WeaponHeat
    {
        private float _timeSinceLastShot = float.MaxValue;
        private float _lockTimer;

        /// <summary>Barrel heat between 0 (cold) and 1 (overheated).</summary>
        public float Heat { get; private set; }

        /// <summary>True while the weapon is locked after overheating.</summary>
        public bool Overheated { get; private set; }

        /// <summary>Raised when the weapon overheats (sound + long haptic in the Unity layer).</summary>
        public event Action OnOverheated;

        /// <summary>Raised when the lock ends and the weapon can fire again.</summary>
        public event Action OnCooledDown;

        private static float SecondsBetweenShots => 1f / Balance.ShotsPerSecond;

        /// <summary>
        /// Advances time. Heat only drops while the trigger is released, so the player has to
        /// decide when to pause.
        /// </summary>
        public void Tick(float deltaTime, bool triggerHeld)
        {
            _timeSinceLastShot += deltaTime;

            if (Overheated)
            {
                _lockTimer -= deltaTime;
                if (_lockTimer <= 0f)
                {
                    Overheated = false;
                    Heat = Balance.HeatAfterOverheat;
                    OnCooledDown?.Invoke();
                }
                return;
            }

            if (!triggerHeld)
            {
                Heat = Math.Max(0f, Heat - Balance.CoolingPerSecond * deltaTime);
            }
        }

        /// <summary>
        /// Tries to fire one shot. Returns false if the weapon is locked or the fire-rate
        /// cooldown has not finished yet.
        /// </summary>
        public bool TryFire()
        {
            if (Overheated || _timeSinceLastShot < SecondsBetweenShots)
            {
                return false;
            }

            _timeSinceLastShot = 0f;
            Heat = Math.Min(1f, Heat + Balance.HeatPerShot);

            if (Heat >= 1f)
            {
                Overheated = true;
                _lockTimer = Balance.OverheatLockSeconds;
                OnOverheated?.Invoke();
            }

            return true;
        }

        /// <summary>Restores a cold weapon for a new match.</summary>
        public void Reset()
        {
            Heat = 0f;
            Overheated = false;
            _lockTimer = 0f;
            _timeSinceLastShot = float.MaxValue;
        }
    }
}
