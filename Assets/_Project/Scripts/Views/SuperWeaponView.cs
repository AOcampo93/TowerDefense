using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The super weapon look while a power up lasts, so the player always knows the gun is supercharged, even when
    /// not firing: green electric arcs crackle from the coils around the barrel out past the muzzle, a glow pulses at
    /// the tip and flares with every bolt, lime sparks stream from it and an electric hum plays. It all starts with a
    /// bright flash, flickers in the last seconds (in step with the blinking coils of the WeaponView) and goes out
    /// when the boost ends. The thick green beam itself is drawn by the WeaponView.
    /// </summary>
    public class SuperWeaponView : MonoBehaviour
    {
        private const int MaxArcPoints = 16;

        [SerializeField] private GameController _game;
        [SerializeField] private WeaponController _weapon;
        [Tooltip("Parent of the glow, the arcs, the sparks and the hum, at the muzzle; hidden when there is no boost.")]
        [SerializeField] private GameObject _effects;
        [Tooltip("The player's head: the glow always faces it.")]
        [SerializeField] private Transform _head;
        [Tooltip("Soft glow quad at the tip of the gun.")]
        [SerializeField] private Transform _glow;
        [Tooltip("Arcs drawn in the local space of the effects (z forward, 0 at the muzzle).")]
        [SerializeField] private LineRenderer[] _arcs;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private AudioSource _hum;
        [SerializeField] private GameSounds _sounds;

        [Header("Arcs (muzzle space)")]
        [Tooltip("The arcs start on the coils: a ring of this radius around the barrel...")]
        [SerializeField] private float _coilRadius = 0.042f;
        [Tooltip("...between these depths behind the muzzle.")]
        [SerializeField] private Vector2 _coilDepth = new Vector2(-0.07f, -0.035f);
        [Tooltip("They end between these distances ahead of the muzzle, around the beam.")]
        [SerializeField] private Vector2 _reach = new Vector2(0.03f, 0.18f);
        [SerializeField] private float _endRadius = 0.035f;
        [Tooltip("How far the zigzag strays from a smooth arc.")]
        [SerializeField] private float _jitter = 0.014f;
        [Tooltip("New shapes per second for each arc.")]
        [SerializeField] private float _arcRate = 16f;

        [Header("Glow")]
        [SerializeField] private float _glowSize = 0.2f;
        [Tooltip("The glow throbs by this part of its size, this many times per second.")]
        [SerializeField] private float _glowPulse = 0.25f;
        [SerializeField] private float _glowPulseRate = 9f;
        [Tooltip("The glow grows this much more for a moment on every bolt.")]
        [SerializeField] private float _shotFlare = 0.7f;
        [SerializeField] private float _shotFlareSeconds = 0.1f;
        [Tooltip("The glow is this many times bigger when a power up is collected, then shrinks to its size.")]
        [SerializeField] private float _startFlare = 4f;
        [SerializeField] private float _startFlareSeconds = 0.45f;

        [Header("End")]
        [Tooltip("Seconds before the boost ends when everything starts flickering, and flickers per second " +
                 "(the same as the coils of the WeaponView).")]
        [SerializeField] private float _endingSeconds = 2f;
        [SerializeField] private float _endingBlinkRate = 5f;
        [SerializeField] private float _humVolume = 0.35f;

        private readonly Vector3[] _points = new Vector3[MaxArcPoints];
        private float[] _arcTimers;
        private float _shotFlareTimer;
        private float _startFlareTimer;

        private void Awake()
        {
            _arcTimers = new float[_arcs.Length];
            _effects.SetActive(false);
        }

        private void OnEnable()
        {
            _weapon.OnShot += HandleShot;
        }

        private void OnDisable()
        {
            _weapon.OnShot -= HandleShot;
        }

        private void Start()
        {
            // The core boost is created in GameController.Awake, so it is only safe to subscribe here.
            _game.Boost.OnStarted += HandleStarted;
            _game.Boost.OnCollected += HandleCollected;
            _game.Boost.OnEnded += HandleEnded;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Boost != null)
            {
                _game.Boost.OnStarted -= HandleStarted;
                _game.Boost.OnCollected -= HandleCollected;
                _game.Boost.OnEnded -= HandleEnded;
            }
        }

        private void Update()
        {
            if (!_effects.activeInHierarchy)
            {
                return;
            }

            float dt = Time.deltaTime;
            _shotFlareTimer = Mathf.Max(0f, _shotFlareTimer - dt);
            _startFlareTimer = Mathf.Max(0f, _startFlareTimer - dt);
            bool on = _game.Boost.TimeLeft >= _endingSeconds || Mathf.Repeat(Time.time * _endingBlinkRate, 1f) < 0.5f;

            float pulse = 1f + _glowPulse * Mathf.Sin(Time.time * _glowPulseRate * 2f * Mathf.PI);
            float startFlare = 1f + (_startFlare - 1f) * (_startFlareTimer / _startFlareSeconds);
            float shotFlare = 1f + _shotFlare * (_shotFlareTimer / _shotFlareSeconds);
            float size = _glowSize * pulse * Mathf.Max(startFlare, shotFlare) * (on ? 1f : 0.3f);
            _glow.localScale = new Vector3(size, size, size);
            _glow.rotation = Quaternion.LookRotation(_glow.position - _head.position);

            for (int i = 0; i < _arcs.Length; i++)
            {
                _arcs[i].enabled = on;
                _arcTimers[i] -= dt;
                if (_arcTimers[i] <= 0f)
                {
                    Reshape(_arcs[i]);
                    _arcTimers[i] = Random.Range(0.6f, 1.4f) / _arcRate;
                }
            }
            _hum.volume = on ? _humVolume : 0.3f * _humVolume;
        }

        // A new random arc: from a point on the coils, bulging out around the barrel, to a point ahead of the muzzle.
        private void Reshape(LineRenderer arc)
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 start = radial * _coilRadius + Vector3.forward * Random.Range(_coilDepth.x, _coilDepth.y);
            float endAngle = angle + Random.Range(-1f, 1f);
            Vector3 endRadial = new Vector3(Mathf.Cos(endAngle), Mathf.Sin(endAngle), 0f);
            Vector3 end = endRadial * Random.Range(0f, _endRadius) + Vector3.forward * Random.Range(_reach.x, _reach.y);

            int count = Mathf.Min(arc.positionCount, MaxArcPoints);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float bulge = Mathf.Sin(t * Mathf.PI);
                _points[i] = Vector3.Lerp(start, end, t) + radial * (0.5f * _coilRadius * bulge) +
                             Random.insideUnitSphere * (_jitter * bulge);
            }
            arc.SetPositions(_points);
        }

        // The boost starts: everything lights up at once with a flash, and the hum begins.
        private void HandleStarted()
        {
            _effects.SetActive(true);
            for (int i = 0; i < _arcs.Length; i++)
            {
                Reshape(_arcs[i]);
            }
            _sparks.Play();
            _hum.clip = _sounds.SuperHum;
            _hum.loop = true;
            _hum.volume = _humVolume;
            _hum.Play();
        }

        // Every power up collected flashes the glow, also one that only makes the boost last longer.
        private void HandleCollected()
        {
            _startFlareTimer = _startFlareSeconds;
        }

        private void HandleEnded()
        {
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _hum.Stop();
            _effects.SetActive(false);
        }

        private void HandleShot(Vector3 from, Vector3 to, bool hitTarget)
        {
            _shotFlareTimer = _shotFlareSeconds;
        }
    }
}
