using System;
using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Turns the core WaveCycle into portals and ships. When portals open it places them where the WaveGenerator
    /// says; when the attack starts it shares the wave's ships between the portals and releases them one by one
    /// (every SpawnIntervalSeconds per portal, only while fewer than the wave's MaxShipsInAir are flying and once
    /// the last ship has flown clear of the portal); when the wave is cleared the portals close.
    /// Ships still waiting inside a portal count as alive, so the wave never ends before its last ship leaves.
    /// </summary>
    public class PortalSpawner : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private ShipSpawner _ships;
        [SerializeField] private PortalView _portalPrefab;
        [Tooltip("Parent of the portals. Its local space is the core space: tower center at the origin.")]
        [SerializeField] private Transform _portalsRoot;

        private readonly List<PortalView> _portals = new List<PortalView>();
        private readonly List<PortalPlacement> _placements = new List<PortalPlacement>();
        private readonly Queue<ShipType>[] _queues = new Queue<ShipType>[Balance.MaxPortals];
        private readonly float[] _releaseTimers = new float[Balance.MaxPortals];
        private ObjectPool<PortalView> _pool;
        private Action<PortalView> _releasePortal;
        private System.Random _random;
        private WaveConfig _wave;
        private int _pending;
        // Portal that gets the next free place in the sky first, so no portal keeps its ships waiting for long.
        private int _firstPortal;

        /// <summary>Portals currently in the sky (read by the radar).</summary>
        public IReadOnlyList<PortalView> Portals => _portals;

        /// <summary>Ships of the current wave still flying or waiting to come out (what the WaveCycle needs).</summary>
        public int ShipsLeft => _ships.AliveCount + _pending;

        private void Awake()
        {
            _pool = new ObjectPool<PortalView>(_portalPrefab, _portalsRoot, Balance.MaxPortals);
            _releasePortal = _pool.Release;
            for (int i = 0; i < _queues.Length; i++)
            {
                _queues[i] = new Queue<ShipType>();
            }
        }

        private void Start()
        {
            // The WaveCycle is created in GameController.Awake, so subscribing is only safe from Start.
            _game.WaveCycle.OnPortalsOpening += HandlePortalsOpening;
            _game.WaveCycle.OnAttackStarted += HandleAttackStarted;
            _game.WaveCycle.OnWaveCleared += HandleWaveCleared;
        }

        private void OnDestroy()
        {
            if (_game != null && _game.WaveCycle != null)
            {
                _game.WaveCycle.OnPortalsOpening -= HandlePortalsOpening;
                _game.WaveCycle.OnAttackStarted -= HandleAttackStarted;
                _game.WaveCycle.OnWaveCleared -= HandleWaveCleared;
            }
        }

        private void Update()
        {
            if (_pending <= 0 || !_game.InFight)
            {
                return;
            }

            // Every portal releases its own ships at a steady pace, so ships arrive from several directions at once,
            // but never more than the wave allows in the air, and never on top of a ship still leaving the portal.
            int count = _portals.Count;
            for (int i = 0; i < count; i++)
            {
                _releaseTimers[i] -= Time.deltaTime;
            }
            for (int k = 0; k < count; k++)
            {
                int i = (_firstPortal + k) % count;
                if (_releaseTimers[i] > 0f || _queues[i].Count == 0 || _ships.AliveCount >= _wave.MaxShipsInAir)
                {
                    continue;
                }
                System.Numerics.Vector3 portal = _placements[i].ToPosition();
                if (!_ships.IsClear(portal))
                {
                    continue;
                }
                _releaseTimers[i] = Balance.SpawnIntervalSeconds;
                _ships.Spawn(_queues[i].Dequeue(), portal, _wave, _random);
                _pending--;
                _firstPortal = (i + 1) % count;
            }
        }

        /// <summary>Closes every portal at once and forgets the ships still waiting (rescue, new match).</summary>
        public void StopAll()
        {
            foreach (Queue<ShipType> queue in _queues)
            {
                queue.Clear();
            }
            _pending = 0;
            foreach (PortalView portal in _portals)
            {
                _pool.Release(portal);
            }
            _portals.Clear();
            _placements.Clear();
        }

        private void HandlePortalsOpening(WaveConfig wave)
        {
            StopAll();
            foreach (PortalPlacement placement in WaveGenerator.PortalPositions(wave))
            {
                PortalView portal = _pool.Get();
                portal.Open(placement.ToPosition().ToUnity(), Balance.PortalOpeningSeconds);
                _portals.Add(portal);
                _placements.Add(placement);
            }
            Debug.Log($"[Game] Wave {wave.Number}: {wave.Portals} portals opening, {wave.TotalShips} ships, up to {wave.MaxShipsInAir} at once");
        }

        private void HandleAttackStarted(WaveConfig wave)
        {
            _wave = wave;
            _random = new System.Random(wave.Seed);

            // Deal the ships out like cards, so every portal gets a fair share of fighters and bombers.
            List<ShipType> types = WaveGenerator.ShipTypes(wave);
            for (int i = 0; i < types.Count; i++)
            {
                _queues[i % _portals.Count].Enqueue(types[i]);
            }
            for (int i = 0; i < _portals.Count; i++)
            {
                _releaseTimers[i] = 0f; // the first ship of each portal comes out right away
            }

            // Counted now, in the same event, so the cycle does not see an empty sky and end the wave next frame.
            _pending = types.Count;
        }

        private void HandleWaveCleared(WaveConfig wave)
        {
            foreach (PortalView portal in _portals)
            {
                portal.Close(_releasePortal);
            }
            _portals.Clear();
            _placements.Clear();
            Debug.Log($"[Game] Wave {wave.Number} cleared");
        }
    }
}
