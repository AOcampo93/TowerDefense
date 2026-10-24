using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Core.Ships;
using AlienInvaders.Views;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace AlienInvaders.Game
{
    /// <summary>
    /// Owns the ship pools and the list of ships in the air. It creates the core ship with the ShipFactory, gives it a
    /// pooled GameObject and turns ship events into game consequences: a shot damages the tower (and blows up its
    /// facade), a destroyed ship counts for the score, a shot down at the city sets a roof on fire. Other systems
    /// (portals, radar) only ask it to spawn ships or read ActiveShips.
    /// </summary>
    public class ShipSpawner : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [Tooltip("Parent of every ship. Its local space is the core space: tower center at the origin.")]
        [SerializeField] private Transform _shipsRoot;
        [SerializeField] private ShipController _fighterPrefab;
        [SerializeField] private ShipController _bomberPrefab;
        [SerializeField] private ExplosionPool _explosions;
        [Tooltip("Small flashes where the bolts land on a hull (the explosion pool is used when this is empty).")]
        [SerializeField] private ExplosionPool _hits;
        [SerializeField] private TowerDamageView _towerDamage;
        [SerializeField] private CityFires _cityFires;
        [SerializeField] private int _prewarmFighters = 40;
        [SerializeField] private int _prewarmBombers = 16;

        [Header("Explosions (meters)")]
        [SerializeField] private float _fighterExplosionSize = 30f;
        [SerializeField] private float _bomberExplosionSize = 60f;
        [Tooltip("Explosions on a ship's hull while the bolts of a burst land.")]
        [SerializeField] private float _fighterHitSize = 7f;
        [SerializeField] private float _bomberHitSize = 11f;

        [Header("Debug")]
        [Tooltip("Editor only: the F key spawns one Fighter during the fight.")]
        [SerializeField] private bool _debugSpawnKey = true;

        private readonly List<ShipController> _active = new List<ShipController>();
        private readonly System.Random _debugRandom = new System.Random(5);
        private ObjectPool<ShipController> _fighters;
        private ObjectPool<ShipController> _bombers;

        /// <summary>Ships currently flying (read by the radar).</summary>
        public IReadOnlyList<ShipController> ActiveShips => _active;

        /// <summary>Number of ships currently flying.</summary>
        public int AliveCount => _active.Count;

        /// <summary>True when the F key may spawn test ships (only read in the editor).</summary>
        public bool DebugSpawnKey => _debugSpawnKey;

        private void Awake()
        {
            _fighters = new ObjectPool<ShipController>(_fighterPrefab, _shipsRoot, _prewarmFighters);
            _bombers = new ObjectPool<ShipController>(_bomberPrefab, _shipsRoot, _prewarmBombers);
        }

#if UNITY_EDITOR
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (DebugSpawnKey && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                SpawnDebugFighter();
            }
        }
#endif

        /// <summary>Creates a ship of the given type at a position in tower space (a portal).</summary>
        public ShipController Spawn(ShipType type, System.Numerics.Vector3 position, WaveConfig wave, System.Random random)
        {
            EnemyShip ship = ShipFactory.Create(type, position, wave, random);
            ShipController controller = PoolFor(type).Get();
            controller.Init(ship, this);
            _active.Add(controller);
            return controller;
        }

        /// <summary>Test helper: one wave 1 Fighter coming out of a random point of the sky.</summary>
        public ShipController SpawnDebugFighter()
        {
            float azimuth = (float)(_debugRandom.NextDouble() * 360.0);
            System.Numerics.Vector3 position = EnemyShip.FromAzimuth(azimuth, Balance.MinPortalDistance, 30f);
            return Spawn(ShipType.Fighter, position, WaveGenerator.Build(1, _debugRandom.Next()), _debugRandom);
        }

        /// <summary>A ship finished aiming without being destroyed: the tower takes the hit.</summary>
        public void HandleShipFired(TowerSide side, float damage, Vector3 impact)
        {
            _game.Tower.ReceiveDamage(side, damage);
            _towerDamage.Impact(impact);
            Debug.Log($"[Game] Tower hit on {side}: -{damage:0.#} -> {_game.Tower.Integrity:0.#} %");
        }

        /// <summary>A bolt hit a ship that survives: part of its hull blows up.</summary>
        public void HandleShipHit(ShipType type, Vector3 point)
        {
            ExplosionPool pool = _hits != null ? _hits : _explosions;
            pool.Spawn(point, type == ShipType.Bomber ? _bomberHitSize : _fighterHitSize);
        }

        /// <summary>Picks a roof below a flying ship for it to fire at. False when there is no city under it.</summary>
        public bool TryPickCityTarget(Vector3 shipPosition, out Vector3 target)
        {
            return _cityFires.TryPickTarget(shipPosition, out target);
        }

        /// <summary>A ship's shot reached the city: the roof blows up and starts burning.</summary>
        public void HandleCityShot(Vector3 target)
        {
            _cityFires.Ignite(target);
        }

        /// <summary>The weapon destroyed a ship: score it, blow it up and return it to its pool.</summary>
        public void HandleShipDestroyed(ShipController ship)
        {
            ShipType type = ship.Ship.Type;
            _game.Score.RegisterKill(type);
            _explosions.Spawn(ship.transform.position,
                type == ShipType.Bomber ? _bomberExplosionSize : _fighterExplosionSize);
            Remove(ship);
        }

        /// <summary>Removes every ship at once, without explosions (rescue, new match).</summary>
        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Remove(_active[i]);
            }
        }

        private void Remove(ShipController ship)
        {
            ObjectPool<ShipController> pool = PoolFor(ship.Ship.Type);
            ship.Despawn();
            _active.Remove(ship);
            pool.Release(ship);
        }

        private ObjectPool<ShipController> PoolFor(ShipType type)
        {
            return type == ShipType.Bomber ? _bombers : _fighters;
        }
    }
}
