using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The fires the alien ships start in the city. Every ship that stays alive keeps firing down at the buildings,
    /// so the fewer ships the player brings down, the more of the city burns: the roof blows up, then fire and a
    /// column of smoke stay there for the rest of the match, and a roof hit again burns bigger. A bomber's bomb is a
    /// huge blast with smaller ones around it and leaves a bigger fire. Fires are made while
    /// loading and reused (no new objects during the fight); a new match puts them all out.
    /// </summary>
    public class CityFires : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private CityGenerator _city;
        [SerializeField] private ExplosionPool _explosions;
        [Tooltip("Fire with its column of smoke.")]
        [SerializeField] private GameObject _firePrefab;
        [SerializeField] private int _maxFires = 30;
        [Tooltip("Ships fire at roofs up to this far from the point right under them (meters).")]
        [SerializeField] private float _reach = 140f;
        [Tooltip("Roofs this close to the tower are spared, so the smoke never hides the view straight down.")]
        [SerializeField] private float _minDistanceFromTower = 35f;
        [SerializeField] private float _explosionSize = 18f;
        [Tooltip("Size of a new fire, and how much bigger a burning roof gets with every new hit.")]
        [SerializeField] private float _startScale = 1.2f;
        [SerializeField] private float _growth = 0.35f;
        [SerializeField] private float _maxScale = 3.5f;

        [Header("Bombs")]
        [Tooltip("Diameter of a bomb's blast (meters), and of the smaller blasts thrown around it.")]
        [SerializeField] private float _bombExplosionSize = 100f;
        [SerializeField] private float _bombDebrisSize = 35f;
        [Tooltip("How far from the bomb the smaller blasts go off (meters).")]
        [SerializeField] private float _bombSpread = 30f;
        [SerializeField] private int _bombDebris = 3;
        [Tooltip("Size of the fire a bomb leaves (and how much a burning roof grows when a bomb lands on it).")]
        [SerializeField] private float _bombFireScale = 2.4f;

        private readonly List<int> _candidates = new List<int>();
        private GameObject[] _fires;
        private int _burning;

        private void Awake()
        {
            _fires = new GameObject[_maxFires];
            for (int i = 0; i < _fires.Length; i++)
            {
                _fires[i] = Instantiate(_firePrefab, _city.Root);
                _fires[i].SetActive(false);
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

        /// <summary>A random roof near the point under the ship, or false if there is none.</summary>
        public bool TryPickTarget(Vector3 shipPosition, out Vector3 target)
        {
            Vector3 local = _city.Root.InverseTransformPoint(shipPosition);
            var under = new Vector2(local.x, local.z);
            IReadOnlyList<Vector3> roofs = _city.Roofs;
            _candidates.Clear();
            for (int i = 0; i < roofs.Count; i++)
            {
                var roof = new Vector2(roofs[i].x, roofs[i].z);
                if (roof.sqrMagnitude >= _minDistanceFromTower * _minDistanceFromTower &&
                    (roof - under).sqrMagnitude <= _reach * _reach)
                {
                    _candidates.Add(i);
                }
            }
            if (_candidates.Count == 0)
            {
                target = default;
                return false;
            }
            target = _city.Root.TransformPoint(roofs[_candidates[Random.Range(0, _candidates.Count)]]);
            return true;
        }

        /// <summary>Blows up the roof at a world position and sets it on fire (or makes its fire bigger).</summary>
        public void Ignite(Vector3 position)
        {
            _explosions.Spawn(position, _explosionSize);
            SetOnFire(position, _startScale, _growth);
        }

        /// <summary>
        /// A bomber's bomb at a world position: one huge blast rising over the roofs with a second one above it, like
        /// a mushroom (big enough to be seen through the haze far below), smaller blasts thrown around it, and a big
        /// fire left behind.
        /// </summary>
        public void Bomb(Vector3 position)
        {
            _explosions.Spawn(position + Vector3.up * (_bombExplosionSize * 0.2f), _bombExplosionSize);
            _explosions.Spawn(position + Vector3.up * (_bombExplosionSize * 0.75f), _bombExplosionSize * 0.6f);
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < _bombDebris; i++)
            {
                float angle = start + i * 360f / _bombDebris + Random.Range(-25f, 25f);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (_bombSpread * Random.Range(0.6f, 1f));
                _explosions.Spawn(position + offset, _bombDebrisSize);
            }
            SetOnFire(position, _bombFireScale, _bombFireScale - _startScale);
        }

        // A new fire on the roof, or a bigger one if the roof already burns.
        private void SetOnFire(Vector3 position, float startScale, float growth)
        {
            Vector3 local = _city.Root.InverseTransformPoint(position);
            for (int i = 0; i < _burning; i++)
            {
                if ((_fires[i].transform.localPosition - local).sqrMagnitude < 25f)
                {
                    Grow(_fires[i].transform, growth);
                    return;
                }
            }
            if (_burning < _fires.Length)
            {
                GameObject fire = _fires[_burning++];
                fire.transform.localPosition = local;
                fire.transform.localScale = Vector3.one * startScale;
                fire.SetActive(true);
            }
            else
            {
                // Every fire is already lit: the city burns harder instead.
                Grow(_fires[Random.Range(0, _fires.Length)].transform, growth);
            }
        }

        private void Grow(Transform fire, float growth)
        {
            fire.localScale = Vector3.one * Mathf.Min(_maxScale, fire.localScale.x + growth);
        }

        private void HandleStateChanged(GameState state)
        {
            // A new match starts with the city intact.
            if (state == GameState.Calibration)
            {
                for (int i = 0; i < _burning; i++)
                {
                    _fires[i].SetActive(false);
                }
                _burning = 0;
            }
        }
    }
}
