using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The damage seen on the tower itself. Every enemy shot that lands blows up a piece of the facade under the
    /// player: a fireball, chunks of concrete falling along the wall and fire with smoke that keeps rising from the
    /// hole. When a floor gives way, clouds of dust burst out of the tower all around; the dust belongs to the world,
    /// so it rushes up past the player while the tower drops, and the fall is seen as well as felt. The tower's final
    /// collapse throws out twice as much.
    /// </summary>
    public class TowerDamageView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private ExplosionPool _explosions;
        [Tooltip("The tower (its center is the axis of the facade).")]
        [SerializeField] private Transform _tower;
        [SerializeField] private float _impactExplosionSize = 9f;

        [Header("Chunks of the facade")]
        [Tooltip("Particle system in the world's leaning space (local simulation), drawing small boxes.")]
        [SerializeField] private ParticleSystem _debris;
        [SerializeField] private int _debrisPerImpact = 14;
        [SerializeField] private Vector2 _debrisSpeed = new Vector2(3f, 9f);
        [SerializeField] private Vector2 _debrisSize = new Vector2(0.3f, 1.2f);

        [Header("Fire and smoke from the holes")]
        [SerializeField] private GameObject _smokePrefab;
        [SerializeField] private int _maxSmokes = 10;
        [SerializeField] private float _smokeScale = 0.08f;

        [Header("Dust when a floor gives way")]
        [Tooltip("Particle system in the world's leaning space (local simulation), drawing soft puffs.")]
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private int _dustPuffs = 70;
        [Tooltip("Height range of the puffs (meters, relative to the player's platform).")]
        [SerializeField] private Vector2 _dustHeight = new Vector2(-24f, -2f);
        [SerializeField] private Vector2 _dustSpeed = new Vector2(2f, 7f);
        [SerializeField] private Vector2 _dustSize = new Vector2(5f, 12f);
        [SerializeField] private Color _dustColor = new Color(0.45f, 0.38f, 0.34f, 0.85f);

        private GameObject[] _smokes;
        private int _nextSmoke;

        private void Awake()
        {
            _smokes = new GameObject[_maxSmokes];
            for (int i = 0; i < _smokes.Length; i++)
            {
                _smokes[i] = Instantiate(_smokePrefab, _tower);
                _smokes[i].transform.localScale = Vector3.one * _smokeScale;
                _smokes[i].SetActive(false);
            }
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // The core tower is created in GameController.Awake, so it is only safe to subscribe here.
            _game.Tower.OnThresholdReached += HandleThreshold;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.Tower != null)
            {
                _game.Tower.OnThresholdReached -= HandleThreshold;
            }
        }

        /// <summary>An enemy shot landed on the facade at a world position.</summary>
        public void Impact(Vector3 point)
        {
            _explosions.Spawn(point, _impactExplosionSize);
            Vector3 outward = Outward(point);
            for (int i = 0; i < _debrisPerImpact; i++)
            {
                Vector3 direction = (outward + Random.insideUnitSphere * 0.8f + Vector3.up * 0.3f).normalized;
                Emit(_debris, point, direction * Random.Range(_debrisSpeed.x, _debrisSpeed.y),
                    Random.Range(_debrisSize.x, _debrisSize.y), Color.white);
            }

            // The oldest hole stops smoking when a new one opens.
            GameObject smoke = _smokes[_nextSmoke];
            _nextSmoke = (_nextSmoke + 1) % _smokes.Length;
            smoke.SetActive(false);
            smoke.transform.position = point;
            smoke.SetActive(true);
        }

        private void HandleThreshold(int threshold)
        {
            int puffs = threshold <= 0 ? _dustPuffs * 2 : _dustPuffs;
            for (int i = 0; i < puffs; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 point = _tower.position + direction * Balance.TowerHalfWidth
                                + Vector3.up * Random.Range(_dustHeight.x, _dustHeight.y);
                Vector3 velocity = direction * Random.Range(_dustSpeed.x, _dustSpeed.y) + Vector3.up * Random.Range(-1f, 1f);
                Emit(_dust, point, velocity, Random.Range(_dustSize.x, _dustSize.y), _dustColor);
            }
        }

        // Particles live in the leaning world, so positions and velocities go into the system's own space.
        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, Color color)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = system.transform.InverseTransformPoint(position),
                velocity = system.transform.InverseTransformDirection(velocity),
                startSize = size,
                startColor = color,
                applyShapeToPosition = false
            };
            system.Emit(emit, 1);
        }

        // Horizontal direction from the tower's axis to a point on its facade.
        private Vector3 Outward(Vector3 point)
        {
            Vector3 outward = point - _tower.position;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        }

        private void HandleStateChanged(GameState state)
        {
            // A new match starts with an intact facade and clean air.
            if (state == GameState.Calibration)
            {
                foreach (GameObject smoke in _smokes)
                {
                    smoke.SetActive(false);
                }
                _debris.Clear();
                _dust.Clear();
            }
        }
    }
}
