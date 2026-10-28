using System;

namespace AlienInvaders.Core
{
    /// <summary>
    /// A control that only acts after it has been held for a moment, so it is never set off by accident: the buttons
    /// of the wrist console must stay under fire for a while (Balance.ConsoleHoldSeconds). Every hit counts for a short
    /// grace time (the gun fires a bolt every few hundredths of a second); while hits keep coming the progress fills,
    /// and when they stop it drains, twice as fast. It acts once, until it is reset.
    /// </summary>
    public class HoldToConfirm
    {
        private readonly float _holdSeconds;
        private readonly float _graceSeconds;
        private float _sinceHit = float.MaxValue;

        /// <summary>A control that needs this many seconds of hits, each one counting for graceSeconds.</summary>
        public HoldToConfirm(float holdSeconds, float graceSeconds)
        {
            _holdSeconds = Math.Max(0.01f, holdSeconds);
            _graceSeconds = Math.Max(0f, graceSeconds);
        }

        /// <summary>How close the control is to acting: 0 untouched, 1 done.</summary>
        public float Progress { get; private set; }

        /// <summary>True once the control has acted.</summary>
        public bool Done { get; private set; }

        /// <summary>True while hits are still coming in.</summary>
        public bool Holding => !Done && _sinceHit <= _graceSeconds;

        /// <summary>A bolt hit the control.</summary>
        public void Hit()
        {
            _sinceHit = 0f;
        }

        /// <summary>Advances the time. Returns true only on the moment the control acts.</summary>
        public bool Tick(float deltaTime)
        {
            if (Done)
            {
                return false;
            }
            _sinceHit += deltaTime;
            float change = _sinceHit <= _graceSeconds ? deltaTime / _holdSeconds : -2f * deltaTime / _holdSeconds;
            Progress = Math.Min(1f, Math.Max(0f, Progress + change));
            if (Progress >= 1f)
            {
                Done = true;
                return true;
            }
            return false;
        }

        /// <summary>Back to untouched (when the control appears again).</summary>
        public void Reset()
        {
            Progress = 0f;
            Done = false;
            _sinceHit = float.MaxValue;
        }
    }
}
