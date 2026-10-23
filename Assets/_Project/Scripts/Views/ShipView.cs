using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox look of an enemy ship. The models have no rig, so the life comes from code: the body bobs and banks
    /// into its turns. While the ship aims, a thin red warning laser grows thicker until the shot (design pillar:
    /// the player is always warned); the shot itself is a short, thick red beam.
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

        private Vector3 _target;
        private float _aimSeconds;
        private float _aimTimer;
        private float _fireTimer;
        private float _bank;
        private float _lastYaw;
        private float _bobOffset;

        /// <summary>Resets the look when the ship leaves a portal (pooled ships are reused).</summary>
        public void Begin()
        {
            HideLasers();
            _bank = 0f;
            _lastYaw = transform.eulerAngles.y;
            // So ships of the same wave do not bob in sync.
            _bobOffset = Random.value * 10f;
        }

        /// <summary>Clears the lasers before the ship goes back to the pool.</summary>
        public void End()
        {
            HideLasers();
        }

        /// <summary>Warning laser towards the impact point for the whole aim time.</summary>
        public void ShowAim(Vector3 target, float seconds)
        {
            _target = target;
            _aimSeconds = Mathf.Max(0.01f, seconds);
            _aimTimer = _aimSeconds;
            _aimLaser.enabled = true;
        }

        /// <summary>The ship fires: the warning laser is replaced by a short heavy beam.</summary>
        public void ShowFire(Vector3 target)
        {
            _target = target;
            _aimLaser.enabled = false;
            _fireLaser.enabled = true;
            _fireTimer = _fireSeconds;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            AnimateBody(dt);

            if (_aimLaser.enabled)
            {
                _aimTimer -= dt;
                float charge = 1f - Mathf.Clamp01(_aimTimer / _aimSeconds);
                _aimLaser.widthMultiplier = Mathf.Lerp(_aimWidthStart, _aimWidthEnd, charge * charge);
                SetLaserPoints(_aimLaser);
            }

            if (_fireLaser.enabled)
            {
                _fireTimer -= dt;
                _fireLaser.widthMultiplier = _fireWidth * Mathf.Clamp01(_fireTimer / _fireSeconds);
                SetLaserPoints(_fireLaser);
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

        private void SetLaserPoints(LineRenderer laser)
        {
            laser.SetPosition(0, _nose.position);
            laser.SetPosition(1, _target);
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
