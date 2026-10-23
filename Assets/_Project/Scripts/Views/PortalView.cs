using System;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox alien portal: a black disc with two glowing purple rings that spin in opposite directions. It grows
    /// open during the portal phase, so the player sees (and hears, with a 3D drone that swells with it) where the next
    /// wave will come from, and shrinks away when the wave is cleared. Pooled by the PortalSpawner.
    /// </summary>
    public class PortalView : MonoBehaviour
    {
        [Tooltip("Disc and rings; scaled from 0 to 1 while the portal opens.")]
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _outerRing;
        [SerializeField] private Transform _innerRing;
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _hum;

        [Header("Feel")]
        [Tooltip("Degrees per second of the outer ring (the inner one spins faster the other way).")]
        [SerializeField] private float _spinSpeed = 45f;
        [SerializeField] private float _pulseRate = 1.2f;
        [SerializeField] private float _pulseAmount = 0.05f;
        [SerializeField] private float _closeSeconds = 1f;

        private float _openSeconds;
        private float _elapsed;
        private float _closeElapsed;
        private bool _closing;
        private Action<PortalView> _onClosed;

        /// <summary>0 while closed, 1 once fully open (used by the radar to pulse the portal).</summary>
        public float Openness { get; private set; }

        /// <summary>Starts opening at a position in tower space, facing the tower.</summary>
        public void Open(Vector3 localPosition, float seconds)
        {
            transform.localPosition = localPosition;
            // The disc faces the player on top of the tower (the origin of the parent space).
            transform.localRotation = Quaternion.LookRotation(-localPosition);
            _openSeconds = Mathf.Max(0.01f, seconds);
            _elapsed = 0f;
            _closing = false;
            _onClosed = null;
            SetOpenness(0f);
            _hum.clip = _sounds.PortalHum;
            _hum.loop = true;
            _hum.Play();
        }

        /// <summary>Shrinks the portal away and calls back when it is gone.</summary>
        public void Close(Action<PortalView> onClosed)
        {
            if (_closing)
            {
                return;
            }
            _closing = true;
            _closeElapsed = 0f;
            _onClosed = onClosed;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _outerRing.Rotate(0f, 0f, _spinSpeed * dt, Space.Self);
            _innerRing.Rotate(0f, 0f, -_spinSpeed * 1.7f * dt, Space.Self);

            if (_closing)
            {
                _closeElapsed += dt;
                float t = Mathf.Clamp01(_closeElapsed / _closeSeconds);
                SetOpenness(Openness * (1f - t));
                if (t >= 1f)
                {
                    _onClosed?.Invoke(this);
                }
                return;
            }

            _elapsed += dt;
            // Slow start, fast finish: the vortex seems to tear the sky open.
            float open = Mathf.Clamp01(_elapsed / _openSeconds);
            SetOpenness(open * open);
        }

        private void SetOpenness(float openness)
        {
            Openness = openness;
            float pulse = 1f + Mathf.Sin(Time.time * _pulseRate * 2f * Mathf.PI) * _pulseAmount * openness;
            _visual.localScale = Vector3.one * Mathf.Max(0.0001f, openness * pulse);
            _hum.volume = openness;
        }
    }
}
