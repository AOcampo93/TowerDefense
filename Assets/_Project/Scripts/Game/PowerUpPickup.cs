using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Glowing cubes floating where a ship blew up (a power up). They tumble and drift up slowly, blink when they are
    /// about to vanish, and a single bolt collects them: the weapon gets stronger for a few seconds. They are shot
    /// like a ship (same layer, IShootable), so the weapon needs no special case. Pooled by the PowerUpSpawner.
    /// </summary>
    public class PowerUpPickup : MonoBehaviour, IShootable
    {
        [Tooltip("The cubes; each one tumbles on its own axis.")]
        [SerializeField] private Transform[] _cubes;
        [Tooltip("Hidden and shown again while blinking.")]
        [SerializeField] private GameObject _look;
        [SerializeField] private float _tumbleDegreesPerSecond = 90f;
        [Tooltip("The whole group turns slowly around its center (degrees per second).")]
        [SerializeField] private float _orbitDegreesPerSecond = 40f;
        [Tooltip("Upward drift (meters per second), so the cubes float out of the smoke of the blast.")]
        [SerializeField] private float _riseSpeed = 1.5f;
        [Tooltip("Seconds before vanishing when the cubes start blinking, and blinks per second.")]
        [SerializeField] private float _blinkSeconds = 1.5f;
        [SerializeField] private float _blinkRate = 8f;

        private PowerUpSpawner _owner;
        private float _timeLeft;

        /// <summary>Places the cubes at a world position for a number of seconds.</summary>
        public void Begin(Vector3 position, float seconds, PowerUpSpawner owner)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            _owner = owner;
            _timeLeft = seconds;
            _look.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timeLeft -= dt;
            if (_timeLeft <= 0f)
            {
                _owner.Expire(this);
                return;
            }

            transform.position += Vector3.up * (_riseSpeed * dt);
            transform.Rotate(0f, _orbitDegreesPerSecond * dt, 0f, Space.Self);
            for (int i = 0; i < _cubes.Length; i++)
            {
                // Every cube tumbles on a different axis, so the group sparkles instead of turning as one block.
                Vector3 axis = new Vector3(i % 2 == 0 ? 1f : -1f, 1f, i % 3 == 0 ? 1f : -0.5f);
                _cubes[i].Rotate(axis, _tumbleDegreesPerSecond * dt, Space.Self);
            }
            _look.SetActive(_timeLeft > _blinkSeconds || Mathf.Repeat(_timeLeft * _blinkRate, 1f) < 0.6f);
        }

        /// <summary>One bolt is enough: the power up is collected.</summary>
        public void ReceiveHit(int damage, Vector3 point)
        {
            if (_owner != null && _timeLeft > 0f)
            {
                _timeLeft = 0f;
                _owner.Collect(this);
            }
        }
    }
}
