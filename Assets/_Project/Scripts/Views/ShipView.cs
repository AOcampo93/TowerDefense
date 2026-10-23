using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox look of an enemy ship. The models have no rig, so the life comes from code: the body bobs and banks
    /// into its turns. While the ship aims, a thin red warning laser grows thicker until the shot (design pillar:
    /// the player is always warned); the shot itself is a short, thick red beam. A health bar floats over the ship,
    /// always turned towards the player, and shrinks from green to red as the bolts land. Sounds are 3D, so a ship
    /// charging behind the player is heard behind them: a quiet engine loop while flying and a charging buzz whose
    /// pitch rises until the shot.
    /// </summary>
    public class ShipView : MonoBehaviour
    {
        [Tooltip("Meshes of the ship. Bobbing and banking move this child, never the ship itself.")]
        [SerializeField] private Transform _model;
        [Tooltip("Where the lasers start.")]
        [SerializeField] private Transform _nose;
        [SerializeField] private LineRenderer _aimLaser;
        [SerializeField] private LineRenderer _fireLaser;

        [Header("Feel")]
        [SerializeField] private float _bobHeight = 0.15f;
        [SerializeField] private float _bobSpeed = 2f;
        [Tooltip("Degrees of roll for every degree per second of turning.")]
        [SerializeField] private float _bankPerTurnRate = 0.3f;
        [SerializeField] private float _maxBank = 40f;
        [SerializeField] private float _aimWidthStart = 0.03f;
        [SerializeField] private float _aimWidthEnd = 0.12f;
        [SerializeField] private float _fireWidth = 0.35f;
        [SerializeField] private float _fireSeconds = 0.2f;

        [Header("Health bar")]
        [Tooltip("Holder of the bar; it turns to face the top of the tower.")]
        [SerializeField] private Transform _healthBar;
        [Tooltip("Colored part of the bar; its pivot is the bar's center, so it is moved as it shrinks.")]
        [SerializeField] private Transform _healthFill;
        [SerializeField] private Renderer _healthFillRenderer;
        [SerializeField] private Color _fullHealthColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] private Color _halfHealthColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color _lowHealthColor = new Color(1f, 0.12f, 0.05f);
        [Tooltip("Where the player's eyes are, in the space of the ships' parent (top of the tower).")]
        [SerializeField] private Vector3 _viewer = new Vector3(0f, 1.6f, 0f);

        [Header("Sound")]
        [SerializeField] private GameSounds _sounds;
        [SerializeField] private AudioSource _engine;
        [Tooltip("Charging buzz and shot.")]
        [SerializeField] private AudioSource _voice;
        [SerializeField] private float _chargePitchStart = 0.8f;
        [SerializeField] private float _chargePitchEnd = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 _aimTarget;
        private Vector3 _fireTarget;
        private MaterialPropertyBlock _healthBlock;
        private Vector3 _fillScale;
        private float _aimSeconds;
        private float _aimTimer;
        private float _fireTimer;
        private float _bank;
        private float _lastYaw;
        private float _bobOffset;

        private void Awake()
        {
            _healthBlock = new MaterialPropertyBlock();
            _fillScale = _healthFill.localScale;
        }

        /// <summary>Resets the look when the ship leaves a portal (pooled ships are reused).</summary>
        public void Begin()
        {
            HideLasers();
            ShowDamage(1f);
            _bank = 0f;
            _lastYaw = transform.eulerAngles.y;
            // So ships of the same wave do not bob (or hum) in sync.
            _bobOffset = Random.value * 10f;
            _engine.clip = _sounds.ShipEngine;
            _engine.loop = true;
            _engine.pitch = Random.Range(0.9f, 1.1f);
            _engine.Play();
        }

        /// <summary>Clears the lasers before the ship goes back to the pool.</summary>
        public void End()
        {
            HideLasers();
            _engine.Stop();
            _voice.Stop();
        }

        /// <summary>Shrinks the health bar to the share of health left (1 = intact) and colors it.</summary>
        public void ShowDamage(float health)
        {
            health = Mathf.Clamp01(health);
            Vector3 scale = _fillScale;
            scale.x *= health;
            _healthFill.localScale = scale;
            // Keep the left end in place: the bar empties from the right.
            _healthFill.localPosition = new Vector3(-_fillScale.x * (1f - health) * 0.5f, 0f, 0f);
            Color color = health > 0.5f
                ? Color.Lerp(_halfHealthColor, _fullHealthColor, (health - 0.5f) * 2f)
                : Color.Lerp(_lowHealthColor, _halfHealthColor, health * 2f);
            _healthBlock.SetColor(BaseColorId, color);
            _healthFillRenderer.SetPropertyBlock(_healthBlock);
        }

        /// <summary>Warning laser towards the impact point for the whole aim time.</summary>
        public void ShowAim(Vector3 target, float seconds)
        {
            _aimTarget = target;
            _aimSeconds = Mathf.Max(0.01f, seconds);
            _aimTimer = _aimSeconds;
            _aimLaser.enabled = true;
            _voice.clip = _sounds.Charge;
            _voice.loop = true;
            _voice.pitch = _chargePitchStart;
            _voice.Play();
        }

        /// <summary>The ship fires (at the tower or down at the city): a short heavy beam.</summary>
        public void ShowFire(Vector3 target)
        {
            _fireTarget = target;
            _aimLaser.enabled = false;
            _fireLaser.enabled = true;
            _fireTimer = _fireSeconds;
            _voice.Stop();
            _voice.loop = false;
            _voice.pitch = 1f;
            _voice.PlayOneShot(_sounds.EnemyShot);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            AnimateBody(dt);
            // The bar always faces the player on top of the tower, whatever way the ship flies.
            Vector3 viewer = transform.parent.TransformPoint(_viewer);
            _healthBar.rotation = Quaternion.LookRotation(_healthBar.position - viewer, Vector3.up);

            if (_aimLaser.enabled)
            {
                _aimTimer -= dt;
                float charge = 1f - Mathf.Clamp01(_aimTimer / _aimSeconds);
                _aimLaser.widthMultiplier = Mathf.Lerp(_aimWidthStart, _aimWidthEnd, charge * charge);
                _voice.pitch = Mathf.Lerp(_chargePitchStart, _chargePitchEnd, charge);
                SetLaserPoints(_aimLaser, _aimTarget);
            }

            if (_fireLaser.enabled)
            {
                _fireTimer -= dt;
                _fireLaser.widthMultiplier = _fireWidth * Mathf.Clamp01(_fireTimer / _fireSeconds);
                SetLaserPoints(_fireLaser, _fireTarget);
                if (_fireTimer <= 0f)
                {
                    _fireLaser.enabled = false;
                }
            }
        }

        private void AnimateBody(float dt)
        {
            // Bank into turns like an aircraft: turning right (yaw growing) lowers the right wing.
            float yaw = transform.eulerAngles.y;
            float turnRate = dt > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
            _lastYaw = yaw;
            float targetBank = Mathf.Clamp(-turnRate * _bankPerTurnRate, -_maxBank, _maxBank);
            _bank = Mathf.Lerp(_bank, targetBank, 1f - Mathf.Exp(-5f * dt));

            float bob = Mathf.Sin((Time.time + _bobOffset) * _bobSpeed) * _bobHeight;
            _model.localPosition = new Vector3(0f, bob, 0f);
            _model.localRotation = Quaternion.Euler(0f, 0f, _bank);
        }

        private void SetLaserPoints(LineRenderer laser, Vector3 target)
        {
            laser.SetPosition(0, _nose.position);
            laser.SetPosition(1, target);
        }

        private void HideLasers()
        {
            _aimLaser.enabled = false;
            _fireLaser.enabled = false;
            _aimTimer = 0f;
            _fireTimer = 0f;
        }
    }
}
