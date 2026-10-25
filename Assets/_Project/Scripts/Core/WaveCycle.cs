using System;

namespace AlienInvaders.Core
{
    /// <summary>
    /// Rhythm of the game: when a wave is cleared there are only a couple of seconds of peace, then the
    /// portals open (visible on the radar, with a countdown) and the ships pour out. The fight is constant: the only
    /// rest is the one the player earns by clearing the sky, and it is short. A wave that is not cleared in time
    /// (Balance.MaxWaveSeconds) lets the next one open its portals while its own ships keep flying.
    /// </summary>
    public class WaveCycle
    {
        private readonly WaveGenerator _generator;
        private float _timer;
        private float _attackSeconds;

        public WavePhase Phase { get; private set; } = WavePhase.Peace;

        /// <summary>Configuration of the wave that is opening or attacking.</summary>
        public WaveConfig CurrentWave { get; private set; }

        /// <summary>Seconds left in the current calm phase (0 while attacking).</summary>
        public float PhaseTimeLeft => Phase == WavePhase.Attacking ? 0f : _timer;

        /// <summary>Raised when portals start opening (Unity layer plays the portal animation).</summary>
        public event Action<WaveConfig> OnPortalsOpening;

        /// <summary>Raised when the ships must start coming out of the portals.</summary>
        public event Action<WaveConfig> OnAttackStarted;

        /// <summary>Raised when every ship of the wave has been destroyed.</summary>
        public event Action<WaveConfig> OnWaveCleared;

        public WaveCycle(WaveGenerator generator)
        {
            _generator = generator;
            _timer = Balance.PeaceSeconds;
        }

        /// <summary>
        /// Advances the cycle. aliveShips is the number of ships of the current wave that are
        /// still flying (including the ones not yet released from the portals).
        /// </summary>
        public void Tick(float deltaTime, int aliveShips)
        {
            switch (Phase)
            {
                case WavePhase.Peace:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        OpenNextWave();
                    }
                    break;

                case WavePhase.PortalsOpening:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        Phase = WavePhase.Attacking;
                        _attackSeconds = 0f;
                        OnAttackStarted?.Invoke(CurrentWave);
                    }
                    break;

                case WavePhase.Attacking:
                    _attackSeconds += deltaTime;
                    if (aliveShips <= 0)
                    {
                        OnWaveCleared?.Invoke(CurrentWave);
                        Phase = WavePhase.Peace;
                        _timer = Balance.PeaceSeconds;
                    }
                    else if (_attackSeconds >= Balance.MaxWaveSeconds)
                    {
                        // Too slow: the next wave comes anyway and the ships still flying stay.
                        OpenNextWave();
                    }
                    break;
            }
        }

        private void OpenNextWave()
        {
            CurrentWave = _generator.Next();
            Phase = WavePhase.PortalsOpening;
            _timer = Balance.PortalOpeningSeconds;
            OnPortalsOpening?.Invoke(CurrentWave);
        }

        /// <summary>Back to the first calm phase before wave 1.</summary>
        public void Reset()
        {
            _generator.Reset();
            Phase = WavePhase.Peace;
            _timer = Balance.PeaceSeconds;
            CurrentWave = default;
        }
    }
}
