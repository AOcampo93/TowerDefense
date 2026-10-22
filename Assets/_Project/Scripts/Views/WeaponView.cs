using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox look and feel of the blaster. The gun appears when the elevator opens, every shot draws a short cyan
    /// beam, the energy coils glow with the heat (cyan, then orange, then red) and blink while the weapon is locked.
    /// The heat is shown on the gun itself instead of a HUD, so the player never looks away from the ships.
    /// Haptics: a soft tick per shot and a long buzz when the weapon overheats.
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
        [SerializeField] private GameObject _muzzleFlash;

        [Header("Feel")]
        [SerializeField] private float _beamSeconds = 0.05f;
        [SerializeField] private Color _coldColor = new Color(0f, 0.85f, 1f);
        [SerializeField] private Color _warmColor = new Color(1f, 0.5f, 0.05f);
        [SerializeField] private Color _hotColor = new Color(1f, 0.05f, 0.03f);
        [SerializeField] private Color _lockedOffColor = new Color(0.15f, 0.02f, 0.02f);
        [Tooltip("Blinks per second of the coils while the weapon is locked.")]
        [SerializeField] private float _lockBlinkRate = 5f;

        [Header("Haptics")]
        [SerializeField] private float _shotPulseAmplitude = 0.3f;
        [SerializeField] private float _shotPulseSeconds = 0.03f;
        [SerializeField] private float _overheatPulseAmplitude = 0.9f;
        [SerializeField] private float _overheatPulseSeconds = 0.6f;

        private MaterialPropertyBlock _coilBlock;
        private Color _shownCoilColor;
        private float _beamTimer;

        private void Awake()
        {
            _coilBlock = new MaterialPropertyBlock();
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
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Weapon != null)
            {
                _game.Weapon.OnOverheated -= HandleOverheated;
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
                if (_beamTimer <= 0f)
                {
                    _beam.enabled = false;
                    _muzzleFlash.SetActive(false);
                }
            }

            SetCoilColor(HeatColor(_game.Weapon));
        }

        private void HandleStateChanged(GameState state)
        {
            // The gun appears while the elevator opens and stays for the whole fight.
            SetVisible(state == GameState.Arrival || state == GameState.Defense ||
                       state == GameState.Respite || state == GameState.Critical);
        }

        private void HandleShot(Vector3 from, Vector3 to, bool hitTarget)
        {
            // One beam is enough: at 8 shots per second a beam is gone long before the next shot.
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, to);
            _beam.enabled = true;
            _muzzleFlash.SetActive(true);
            _beamTimer = _beamSeconds;
            ControllerHaptics.PulseRight(_shotPulseAmplitude, _shotPulseSeconds);
        }

        private void HandleOverheated()
        {
            ControllerHaptics.PulseRight(_overheatPulseAmplitude, _overheatPulseSeconds);
        }

        private Color HeatColor(WeaponHeat heat)
        {
            if (heat.Overheated)
            {
                bool blinkOn = Mathf.Repeat(Time.time * _lockBlinkRate, 1f) < 0.5f;
                return blinkOn ? _hotColor : _lockedOffColor;
            }

            // Cold to warm in the first half of the heat bar, warm to hot in the second half.
            return heat.Heat < 0.5f
                ? Color.Lerp(_coldColor, _warmColor, heat.Heat / 0.5f)
                : Color.Lerp(_warmColor, _hotColor, (heat.Heat - 0.5f) / 0.5f);
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
            _muzzleFlash.SetActive(false);
            _beamTimer = 0f;
        }
    }
}
