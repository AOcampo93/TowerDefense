using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Look of an enemy ship. Each time a ship leaves a portal it wears one of several hull models, so a wave is not a
    /// row of clones. The ships fly like alien craft: no bobbing and no banking, they glide level and turn smoothly.
    /// While the ship aims, a thin red warning laser grows thicker until the shot (design pillar:
    /// the player is always warned); the shot itself is a short, thick red beam. A health bar floats over the ship,
    /// always turned towards the player, and shrinks from green to red as the bolts land. Sounds are 3D, so a ship
    /// charging behind the player is heard behind them: a quiet engine loop while flying and a charging buzz whose
    /// pitch rises until the shot.
    /// </summary>
    public class ShipView : MonoBehaviour
    {
        [Tooltip("Hull models; one of them is shown each time the ship leaves a portal. Each has a child named Nose " +
                 "where the lasers start.")]
        [SerializeField] private GameObject[] _hulls;
        [SerializeField] private LineRenderer _aimLaser;
        [SerializeField] private LineRenderer _fireLaser;

        [Header("Lasers")]
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

        private Transform[] _noses;
        private Transform _nose;
        private Vector3 _aimTarget;
        private Vector3 _fireTarget;
        private MaterialPropertyBlock _healthBlock;
        private Vector3 _fillScale;
        private float _aimSeconds;
        private float _aimTimer;
        private float _fireTimer;

        private void Awake()
        {
            _healthBlock = new MaterialPropertyBlock();
            _fillScale = _healthFill.localScale;
            _noses = new Transform[_hulls.Length];
            for (int i = 0; i < _hulls.Length; i++)
            {
                _noses[i] = _hulls[i].transform.Find("Nose");
            }
        }

        /// <summary>Resets the look when the ship leaves a portal (pooled ships are reused).</summary>
        public void Begin()
        {
            HideLasers();
            ShowDamage(1f);
            int hull = Random.Range(0, _hulls.Length);
            for (int i = 0; i < _hulls.Length; i++)
            {
                _hulls[i].SetActive(i == hull);
            }
            _nose = _noses[hull];
            // So ships of the same wave do not hum in sync.
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
