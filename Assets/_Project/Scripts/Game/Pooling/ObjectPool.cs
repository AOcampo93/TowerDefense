using System.Collections.Generic;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Reuses copies of a prefab instead of creating and destroying them during the fight. Instantiate and Destroy
    /// allocate memory and wake up the garbage collector, which causes frame hitches, and in VR a hitch is felt as
    /// discomfort. The copies are made up front (prewarm); the pool only grows if a wave needs more than that.
    /// It is generic, so the same class pools ships, explosions and portals.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free = new Stack<T>();

        /// <summary>Copies currently in use.</summary>
        public int ActiveCount { get; private set; }

        /// <param name="prefab">Template to copy.</param>
        /// <param name="parent">Transform that holds every copy.</param>
        /// <param name="prewarm">Copies created right away, while the game is still loading.</param>
        public ObjectPool(T prefab, Transform parent, int prewarm)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                _free.Push(CreateCopy());
            }
        }

        /// <summary>Takes a copy from the pool (creating one only if the pool is empty) and activates it.</summary>
        public T Get()
        {
            T item = _free.Count > 0 ? _free.Pop() : CreateCopy();
            item.gameObject.SetActive(true);
            ActiveCount++;
            return item;
        }

        /// <summary>Deactivates a copy and keeps it for the next Get.</summary>
        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            _free.Push(item);
            ActiveCount--;
        }

        private T CreateCopy()
        {
            T item = Object.Instantiate(_prefab, _parent);
            item.name = _prefab.name;
            item.gameObject.SetActive(false);
            return item;
        }
    }
}
