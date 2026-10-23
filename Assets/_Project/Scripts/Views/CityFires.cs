using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The fires the alien ships start in the city. Every ship that stays alive keeps firing down at the buildings,
    /// so the fewer ships the player brings down, the more of the city burns: the roof blows up, then fire and a
    /// column of smoke stay there for the rest of the match, and a roof hit again burns bigger. Fires are made while
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
            Vector3 local = _city.Root.InverseTransformPoint(position);
            for (int i = 0; i < _burning; i++)
            {
                if ((_fires[i].transform.localPosition - local).sqrMagnitude < 25f)
                {
                    Grow(_fires[i].transform);
                    return;
                }
            }
            if (_burning < _fires.Length)
            {
                GameObject fire = _fires[_burning++];
                fire.transform.localPosition = local;
                fire.transform.localScale = Vector3.one * _startScale;
                fire.SetActive(true);
            }
            else
            {
                // Every fire is already lit: the city burns harder instead.
                Grow(_fires[Random.Range(0, _fires.Length)].transform);
            }
        }

        private void Grow(Transform fire)
        {
            fire.localScale = Vector3.one * Mathf.Min(_maxScale, fire.localScale.x + _growth);
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
