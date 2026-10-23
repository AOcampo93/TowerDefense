using System;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Single entry point for explosions (destroyed ships, impacts on the facade). Explosions are reused from a pool
    /// because a busy wave can create several per second.
    /// </summary>
    public class ExplosionPool : MonoBehaviour
    {
        [SerializeField] private ExplosionView _prefab;
        [Tooltip("Parent of the explosions; it belongs to the virtual world so they hide with it.")]
        [SerializeField] private Transform _root;
        [SerializeField] private int _prewarm = 16;

        private ObjectPool<ExplosionView> _pool;
        private Action<ExplosionView> _release;

        private void Awake()
        {
            _pool = new ObjectPool<ExplosionView>(_prefab, _root, _prewarm);
            // One cached delegate instead of a new one per explosion (no garbage during the fight).
            _release = _pool.Release;
        }

        /// <summary>Plays an explosion at a world position. Size is the final diameter in meters.</summary>
        public void Spawn(Vector3 position, float size)
        {
            _pool.Get().Play(position, size, _release);
        }
    }
}
