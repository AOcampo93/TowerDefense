using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Puts power ups in the sky and collects them. The core WeaponBoost decides which ship brought down leaves one;
    /// this places the glowing cubes where it blew up, and when the player shoots them it starts the boost and cools
    /// the weapon. The cubes are pooled, and all of them vanish when the fight ends.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private PowerUpPickup _prefab;
        [Tooltip("Parent of the cubes; it leans with the sky like the ships and the explosions.")]
        [SerializeField] private Transform _root;
        [Tooltip("Burst where the cubes are collected (optional).")]
        [SerializeField] private ExplosionPool _burst;
        [SerializeField] private float _burstSize = 14f;
        [SerializeField] private int _prewarm = 3;

        private readonly List<PowerUpPickup> _active = new List<PowerUpPickup>();
        private ObjectPool<PowerUpPickup> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<PowerUpPickup>(_prefab, _root, _prewarm);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        /// <summary>Glowing cubes at a world position, for Balance.PowerUpFloatSeconds.</summary>
        public void Spawn(Vector3 position)
        {
            PowerUpPickup pickup = _pool.Get();
            pickup.Begin(position, Balance.PowerUpFloatSeconds, this);
            _active.Add(pickup);
        }

        /// <summary>The player shot the cubes: a stronger weapon, cold and ready, for a few seconds.</summary>
        public void Collect(PowerUpPickup pickup)
        {
            if (_burst != null)
            {
                _burst.Spawn(pickup.transform.position, _burstSize);
            }
            _game.Weapon.Reset();
            _game.Boost.Collect();
            Release(pickup);
        }

        /// <summary>The cubes were not shot in time and vanish.</summary>
        public void Expire(PowerUpPickup pickup)
        {
            Release(pickup);
        }

        private void Release(PowerUpPickup pickup)
        {
            if (_active.Remove(pickup))
            {
                _pool.Release(pickup);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (_game.InFight)
            {
                return;
            }
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Release(_active[i]);
            }
        }
    }
}
