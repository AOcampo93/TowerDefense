using AlienInvaders.Core;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Glowing cubes floating where a ship blew up (a power up). They pop out of the blast a moment after it, with a
    /// flare and a chime, so the player notices them, then tumble and drift up slowly. Several bolts collect them: each
    /// hit makes them swell, pulls them closer together, spins them faster and rings a higher note, so the player sees
    /// the power up filling. They blink when they are about to vanish. They are shot like a ship (same layer,
    /// IShootable), so the weapon needs no special case. The rules (when they appear, how many bolts) live in the
    /// core PowerUpDrop. Pooled by the PowerUpSpawner.
    /// </summary>
    public class PowerUpPickup : MonoBehaviour, IShootable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("The cubes; each one tumbles on its own axis.")]
        [SerializeField] private Transform[] _cubes;
        [Tooltip("Hidden while the cubes are still inside the blast, and while blinking.")]
        [SerializeField] private GameObject _look;
        [Tooltip("Glow around the cubes; it flares when they appear and with every hit.")]
        [SerializeField] private Renderer _halo;
        [Tooltip("Off until the cubes come out of the blast, so the bolts pass through.")]
        [SerializeField] private Collider _collider;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private float _tumbleDegreesPerSecond = 90f;
        [Tooltip("The whole group turns slowly around its center (degrees per second).")]
        [SerializeField] private float _orbitDegreesPerSecond = 40f;
        [Tooltip("Upward drift (meters per second), so the cubes float out of the smoke of the blast.")]
        [SerializeField] private float _riseSpeed = 1.5f;
        [Tooltip("Seconds before vanishing when the cubes start blinking, and blinks per second.")]
        [SerializeField] private float _blinkSeconds = 1.5f;
        [SerializeField] private float _blinkRate = 8f;

        [Header("Appear and fill")]
        [Tooltip("Seconds of the pop when the cubes come out of the blast; they grow past their size and settle back.")]
        [SerializeField] private float _popSeconds = 0.45f;
        [SerializeField] private float _popOvershoot = 1.4f;
        [Tooltip("Each hit makes the cubes swell this much for a moment.")]
        [SerializeField] private float _hitPunch = 0.35f;
        [SerializeField] private float _hitPunchSeconds = 0.18f;
        [Tooltip("When full, the cubes are pulled this much towards the core (0 = not at all, 1 = into the core).")]
        [SerializeField] private float _pullIn = 0.6f;
        [Tooltip("When full, the cubes tumble this many times faster.")]
        [SerializeField] private float _fullSpin = 4f;
        [Tooltip("The halo grows this much when full.")]
        [SerializeField] private float _fullHaloGrowth = 0.6f;
        [Tooltip("The halo gets this much more opaque when full, and when the cubes pop out or take a hit.")]
        [SerializeField] private float _haloBrightening = 0.6f;
        [SerializeField] private float _haloFlare = 0.8f;
        [Tooltip("The hit note rises this much in pitch from the first bolt to the last.")]
        [SerializeField] private float _hitPitchRise = 0.9f;
        [SerializeField] private float _appearVolume = 1f;
        [SerializeField] private float _hitVolume = 0.8f;

        private readonly PowerUpDrop _drop = new PowerUpDrop();
        private PowerUpSpawner _owner;
        private Vector3[] _cubeHomes;
        private Vector3 _lookScale;
        private Vector3 _haloScale;
        private Color _haloColor;
        private MaterialPropertyBlock _haloBlock;
        private float _shownCharge;
        private float _punch;
        private bool _shown;

        private void Awake()
        {
            _cubeHomes = new Vector3[_cubes.Length];
            for (int i = 0; i < _cubes.Length; i++)
            {
                _cubeHomes[i] = _cubes[i].localPosition;
            }
            _lookScale = _look.transform.localScale;
            _haloScale = _halo.transform.localScale;
            _haloColor = _halo.sharedMaterial.GetColor(BaseColorId);
            _haloBlock = new MaterialPropertyBlock();
        }

        /// <summary>A ship blew up at a world position: the cubes will come out of its blast in a moment.</summary>
        public void Begin(Vector3 position, PowerUpSpawner owner)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            _owner = owner;
            _drop.Reset();
            _shown = false;
            _shownCharge = 0f;
            _punch = 0f;
            _look.SetActive(false);
            _collider.enabled = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _drop.Tick(dt);
            if (_drop.Gone)
            {
                _owner.Expire(this);
                return;
            }
            if (!_drop.Visible)
            {
                return;
            }
            if (!_shown)
            {
                Show();
            }

            _shownCharge = Mathf.MoveTowards(_shownCharge, _drop.Charge, dt * 4f);
            _punch = Mathf.Max(0f, _punch - dt / _hitPunchSeconds);

            transform.position += Vector3.up * (_riseSpeed * dt);
            transform.Rotate(0f, _orbitDegreesPerSecond * dt, 0f, Space.Self);
            float tumble = _tumbleDegreesPerSecond * (1f + (_fullSpin - 1f) * _shownCharge) * dt;
            float pull = 1f - _pullIn * _shownCharge;
            for (int i = 0; i < _cubes.Length; i++)
            {
                // Every cube tumbles on a different axis, so the group sparkles instead of turning as one block.
                Vector3 axis = new Vector3(i % 2 == 0 ? 1f : -1f, 1f, i % 3 == 0 ? 1f : -0.5f);
                _cubes[i].Rotate(axis, tumble, Space.Self);
                _cubes[i].localPosition = _cubeHomes[i] * pull;
            }

            float pop = PopScale(_drop.ShownSeconds / _popSeconds);
            float flare = Mathf.Max(_punch, 1f - _drop.ShownSeconds / _popSeconds);
            _look.transform.localScale = _lookScale * (pop * (1f + _hitPunch * _punch));
            _halo.transform.localScale = _haloScale * (1f + _fullHaloGrowth * _shownCharge + 0.25f * flare);
            Color halo = _haloColor;
            halo.a = Mathf.Clamp01(_haloColor.a * (1f + _haloBrightening * _shownCharge + _haloFlare * flare));
            _haloBlock.SetColor(BaseColorId, halo);
            _halo.SetPropertyBlock(_haloBlock);

            _look.SetActive(_drop.TimeLeft > _blinkSeconds || Mathf.Repeat(_drop.TimeLeft * _blinkRate, 1f) < 0.6f);
        }

        // The cubes come out of the blast: they can be shot now, and a chime says so.
        private void Show()
        {
            _shown = true;
            _look.SetActive(true);
            _collider.enabled = true;
            _audio.pitch = 1f;
            _audio.PlayOneShot(_sounds.PowerUpAppear, _appearVolume);
        }

        // Grows from nothing past its size, then settles back (t: 0 at the start of the pop, 1 at its end).
        private float PopScale(float t)
        {
            if (t >= 1f)
            {
                return 1f;
            }
            return t < 0.6f ? _popOvershoot * (t / 0.6f) : Mathf.Lerp(_popOvershoot, 1f, (t - 0.6f) / 0.4f);
        }

        /// <summary>A bolt hit the cubes: they fill a little, and the last bolt collects them.</summary>
        public void ReceiveHit(int damage, Vector3 point)
        {
            if (_owner == null || !_drop.Visible)
            {
                return;
            }
            if (_drop.Hit())
            {
                _owner.Collect(this);
                return;
            }
            _punch = 1f;
            _audio.pitch = 1f + _hitPitchRise * _drop.Charge;
            _audio.PlayOneShot(_sounds.PowerUpHit, _hitVolume);
        }
    }
}
