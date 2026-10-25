using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Look and feel of the blaster, a big retro ray gun almost half an arm long (Cosmic Retro blaster model). The gun
    /// appears when the elevator opens, every bolt of a burst draws a thick cyan beam with a white hot core, the energy
    /// coils around its muzzle glow with the heat (cyan, then orange, then red) and blink while the weapon is locked.
    /// The heat is shown on the gun itself instead of a HUD, so the player never looks away from the ships.
    /// While a power up lasts, the beam turns lime green and several times thicker, and each beam lasts until the next
    /// bolt, so holding the trigger draws one continuous super laser; the coils glow the same color, blinking when the
    /// boost is about to end (the arcs, glow and hum around the muzzle are the SuperWeaponView).
    /// Haptics: a firm tick per shot, a full strength kick when the weapon overheats and then a pulse on every blink
    /// of the coils until the lock ends, so the hand knows the weapon is still locked without looking at it.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameController _game;
        [SerializeField] private WeaponController _weapon;
        [Tooltip("Meshes of the gun, hidden while the player is in the real room or inside the elevator.")]
        [SerializeField] private GameObject _model;
        [Tooltip("Coils that glow with the heat color.")]
        [SerializeField] private Renderer[] _heatCoils;
        [SerializeField] private LineRenderer _beam;
        [Tooltip("White hot center of the beam, drawn over the colored glow.")]
        [SerializeField] private LineRenderer _beamCore;
        [SerializeField] private GameObject _muzzleFlash;
        [Tooltip("Burst of sparks at the muzzle on every shot (optional store asset).")]
        [SerializeField] private GameObject _muzzleSparksPrefab;
        [SerializeField] private float _muzzleSparksScale = 0.12f;

        [Header("Feel")]
        [Tooltip("Seconds a beam stays visible; it thins out while it fades.")]
        [SerializeField] private float _beamSeconds = 0.08f;
        [SerializeField] private float _beamWidth = 0.035f;
        [SerializeField] private float _beamCoreWidth = 0.012f;
        [Tooltip("Coil color from cold (left) to about to overheat (right). The yellow step avoids the grey-green " +
                 "that a straight blend from cyan to orange goes through.")]
        [SerializeField] private Gradient _heatColors = DefaultHeatColors();
        [SerializeField] private Color _hotColor = new Color(1f, 0.05f, 0.03f);
        [SerializeField] private Color _lockedOffColor = new Color(0.15f, 0.02f, 0.02f);
        [Tooltip("Blinks per second of the coils while the weapon is locked.")]
        [SerializeField] private float _lockBlinkRate = 5f;

        [Header("Power up")]
        [SerializeField] private Color _boostBeamColor = new Color(0.45f, 1f, 0.1f);
        [SerializeField] private Color _boostCoreColor = new Color(0.9f, 1f, 0.7f);
        [Tooltip("Width of the boosted beam in meters, at the muzzle (time 0) and at the far end (time 1): fat from the " +
                 "gun on, and much wider far away so it still looks huge next to the ships.")]
        [SerializeField] private AnimationCurve _boostBeamWidths = AnimationCurve.Linear(0f, 0.07f, 1f, 1.6f);
        [SerializeField] private AnimationCurve _boostCoreWidths = AnimationCurve.Linear(0f, 0.025f, 1f, 0.5f);
        [Tooltip("Seconds a boosted beam stays: longer than the time between bolts, so the beams join into one laser.")]
        [SerializeField] private float _boostBeamSeconds = 0.13f;
        [Tooltip("A boosted beam only thins down to this part of its width before the next bolt replaces it.")]
        [SerializeField] private float _boostFadeFloor = 0.65f;
        [Tooltip("Shot tick in the hand while boosted, stronger than the normal one.")]
        [SerializeField] private float _boostShotPulseAmplitude = 1f;
        [Tooltip("Seconds before the boost ends when the coils start blinking.")]
        [SerializeField] private float _boostEndingSeconds = 2f;
        [SerializeField] private float _boostPulseAmplitude = 1f;
        [SerializeField] private float _boostPulseSeconds = 0.3f;

        [Header("Haptics")]
        [SerializeField] private float _shotPulseAmplitude = 0.7f;
        [SerializeField] private float _shotPulseSeconds = 0.05f;
        [Tooltip("Kick the moment the weapon overheats.")]
        [SerializeField] private float _overheatPulseAmplitude = 1f;
        [SerializeField] private float _overheatPulseSeconds = 0.6f;
        [Tooltip("Pulse on every blink of the coils after the kick, for the rest of the lock.")]
        [SerializeField] private float _lockPulseAmplitude = 0.8f;

        [Header("Sound")]
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private float _shotVolume = 0.5f;

        private MaterialPropertyBlock _coilBlock;
        private MaterialPropertyBlock _beamBlock;
        private MaterialPropertyBlock _beamCoreBlock;
        private MaterialPropertyBlock _plainBlock;
        private AnimationCurve _beamWidths;
        private AnimationCurve _beamCoreWidths;
        private float _beamWidthNow;
        private float _beamCoreWidthNow;
        private float _beamDuration;
        private ParticleEffect _muzzleSparks;
        private Color _shownCoilColor;
        private float _beamTimer;
        private float _overheatKickTimer;
        private bool _wasBlinkOn;

        private void Awake()
        {
            _coilBlock = new MaterialPropertyBlock();
            _beamBlock = new MaterialPropertyBlock();
            _beamBlock.SetColor(BaseColorId, _boostBeamColor);
            _beamCoreBlock = new MaterialPropertyBlock();
            _beamCoreBlock.SetColor(BaseColorId, _boostCoreColor);
            _plainBlock = new MaterialPropertyBlock();
            _beamWidths = _beam.widthCurve;
            _beamCoreWidths = _beamCore.widthCurve;
            _beamWidthNow = _beamWidth;
            _beamCoreWidthNow = _beamCoreWidth;
            _muzzleSparks = ParticleEffect.Attach(_muzzleSparksPrefab, _muzzleFlash.transform.parent, _muzzleSparksScale, false);
            SetVisible(false);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
            _weapon.OnShot += HandleShot;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
            _weapon.OnShot -= HandleShot;
        }

        private void Start()
        {
            // The core weapon is created in GameController.Awake, so it is only safe to subscribe here.
            _game.Weapon.OnOverheated += HandleOverheated;
            _game.Boost.OnStarted += HandleBoostStarted;
            _game.Boost.OnEnded += HandleBoostEnded;
            _game.Boost.OnCollected += HandleBoostCollected;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Weapon != null)
            {
                _game.Weapon.OnOverheated -= HandleOverheated;
            }
            if (_game != null && _game.Boost != null)
            {
                _game.Boost.OnStarted -= HandleBoostStarted;
                _game.Boost.OnEnded -= HandleBoostEnded;
                _game.Boost.OnCollected -= HandleBoostCollected;
            }
        }

        private void Update()
        {
            if (!_model.activeSelf)
            {
                return;
            }

            if (_beamTimer > 0f)
            {
                _beamTimer -= Time.deltaTime;
                float left = Mathf.Clamp01(_beamTimer / _beamDuration);
                if (_game.Boost.Active)
                {
                    left = Mathf.Lerp(_boostFadeFloor, 1f, left);
                }
                _beam.widthMultiplier = _beamWidthNow * left;
                _beamCore.widthMultiplier = _beamCoreWidthNow * left;
                if (_beamTimer <= 0f)
                {
                    _beam.enabled = false;
                    _beamCore.enabled = false;
                    _muzzleFlash.SetActive(false);
                }
            }

            // The coils and the lock pulses share one blink, so the hand feels exactly what the eyes see.
            bool blinkOn = _game.Weapon.Overheated && Mathf.Repeat(Time.time * _lockBlinkRate, 1f) < 0.5f;
            UpdateLockHaptics(blinkOn);
            SetCoilColor(_game.Boost.Active ? BoostColor() : HeatColor(_game.Weapon, blinkOn));
        }

        private void HandleStateChanged(GameState state)
        {
            // The gun appears while the elevator opens and stays for the whole fight.
            SetVisible(state == GameState.Arrival || state == GameState.Defense ||
                       state == GameState.Respite || state == GameState.Critical);
        }

        private void HandleShot(Vector3 from, Vector3 to, bool hitTarget)
        {
            // One beam is enough: at 10 bolts per second a beam is gone before the next bolt.
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, to);
            _beam.widthMultiplier = _beamWidthNow;
            _beam.enabled = true;
            _beamCore.SetPosition(0, from);
            _beamCore.SetPosition(1, to);
            _beamCore.widthMultiplier = _beamCoreWidthNow;
            _beamCore.enabled = true;
            _muzzleFlash.SetActive(true);
            _muzzleSparks?.Restart();
            _beamDuration = _game.Boost.Active ? _boostBeamSeconds : _beamSeconds;
            _beamTimer = _beamDuration;
            _audio.PlayOneShot(_sounds.PlayerShot, _shotVolume);

            // The shot that overheats the weapon already fired the overheat kick (OnOverheated comes first), and a
            // new pulse replaces the one in progress, so the short shot tick would cut the kick short.
            if (!_game.Weapon.Overheated)
            {
                float amplitude = _game.Boost.Active ? _boostShotPulseAmplitude : _shotPulseAmplitude;
                ControllerHaptics.PulseRight(amplitude, _shotPulseSeconds);
            }
        }

        private void HandleOverheated()
        {
            ControllerHaptics.PulseRight(_overheatPulseAmplitude, _overheatPulseSeconds);
            _audio.PlayOneShot(_sounds.Overheat);
            _overheatKickTimer = _overheatPulseSeconds;
        }

        // The boost starts: green, much thicker beam (its widths come from the curves in meters, multiplier 1).
        private void HandleBoostStarted()
        {
            _beam.widthCurve = _boostBeamWidths;
            _beamCore.widthCurve = _boostCoreWidths;
            _beamWidthNow = 1f;
            _beamCoreWidthNow = 1f;
            _beam.SetPropertyBlock(_beamBlock);
            _beamCore.SetPropertyBlock(_beamCoreBlock);
        }

        // Every power up collected (also one that only makes the boost last longer): a charge up and a kick in the hand.
        private void HandleBoostCollected()
        {
            _audio.PlayOneShot(_sounds.PowerUp);
            ControllerHaptics.PulseRight(_boostPulseAmplitude, _boostPulseSeconds);
        }

        private void HandleBoostEnded()
        {
            _beam.widthCurve = _beamWidths;
            _beamCore.widthCurve = _beamCoreWidths;
            _beamWidthNow = _beamWidth;
            _beamCoreWidthNow = _beamCoreWidth;
            _beam.SetPropertyBlock(_plainBlock);
            _beamCore.SetPropertyBlock(_plainBlock);
        }

        // Boost color on the coils, blinking in its last seconds so the player knows it is about to end.
        private Color BoostColor()
        {
            bool ending = _game.Boost.TimeLeft < _boostEndingSeconds;
            bool on = !ending || Mathf.Repeat(Time.time * _lockBlinkRate, 1f) < 0.5f;
            return on ? _boostBeamColor : _lockedOffColor;
        }

        private void UpdateLockHaptics(bool blinkOn)
        {
            bool blinkStarted = blinkOn && !_wasBlinkOn;
            _wasBlinkOn = blinkOn;

            // The blink pulses wait until the kick is over, for the same reason as above.
            if (_overheatKickTimer > 0f)
            {
                _overheatKickTimer -= Time.deltaTime;
                return;
            }

            if (blinkStarted)
            {
                ControllerHaptics.PulseRight(_lockPulseAmplitude, 0.5f / _lockBlinkRate);
            }
        }

        private Color HeatColor(WeaponHeat heat, bool blinkOn)
        {
            if (heat.Overheated)
            {
                return blinkOn ? _hotColor : _lockedOffColor;
            }

            return _heatColors.Evaluate(heat.Heat);
        }

        // Cyan when cold, then white, yellow, orange and red as the weapon heats up.
        private static Gradient DefaultHeatColors()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0f, 0.85f, 1f), 0f),
                    new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0.3f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.55f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.05f, 0.03f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private void SetCoilColor(Color color)
        {
            if (color == _shownCoilColor)
            {
                return;
            }

            // A property block tints the coils without creating material copies at runtime.
            _shownCoilColor = color;
            _coilBlock.SetColor(BaseColorId, color);
            foreach (Renderer coil in _heatCoils)
            {
                coil.SetPropertyBlock(_coilBlock);
            }
        }

        private void SetVisible(bool visible)
        {
            _model.SetActive(visible);
            _beam.enabled = false;
            _beamCore.enabled = false;
            _muzzleFlash.SetActive(false);
            _beamTimer = 0f;
        }
    }
}
