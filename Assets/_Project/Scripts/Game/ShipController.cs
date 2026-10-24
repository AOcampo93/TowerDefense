using AlienInvaders.Core;
using AlienInvaders.Core.Ships;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Bridge between one core EnemyShip (the rules: life cycle, health, when to fire) and its GameObject. Every frame
    /// it advances the core ship and copies its position to the transform. Core events become visuals in the ShipView
    /// and damage in the ShipSpawner. While a ship flies between stations it also fires down at the city now and
    /// then (only for show: it sets buildings on fire but does not hurt the tower), so the longer the ships live, the
    /// more the city burns. Ships are pooled, so Init and Despawn take the place of Awake and Destroy.
    /// </summary>
    public class ShipController : MonoBehaviour, IShootable
    {
        [SerializeField] private ShipView _view;
        [Tooltip("Seconds the ship takes to turn most of the way towards where it flies (or aims): slow, smooth turns.")]
        [SerializeField] private float _turnSeconds = 0.6f;
        [Tooltip("Seconds between the shots a flying ship fires down at the city (random in this range).")]
        [SerializeField] private Vector2 _cityShotInterval = new Vector2(7f, 14f);
        [Tooltip("Shortest time between two hit explosions on this ship (a burst lands ten bolts per second).")]
        [SerializeField] private float _hitFlashInterval = 0.12f;

        private ShipSpawner _spawner;
        private float _cityShotTimer;
        private float _hitFlashTimer;

        /// <summary>The core ship shown by this GameObject (null while it waits in the pool).</summary>
        public EnemyShip Ship { get; private set; }

        /// <summary>
        /// World position of the facade point the ship is aiming at. Read through the tower's space, not ShipsRoot:
        /// ShipsRoot leans with the sky, the tower does not.
        /// </summary>
        public Vector3 TargetWorld => _spawner.TowerSpace.TransformPoint(Ship.TargetPoint.ToUnity());

        /// <summary>Links a freshly created core ship to this pooled GameObject and places it at the portal.</summary>
        public void Init(EnemyShip ship, ShipSpawner spawner)
        {
            Ship = ship;
            _spawner = spawner;
            Ship.OnAimStarted += HandleAimStarted;
            Ship.OnFire += HandleFire;
            Ship.OnDestroyed += HandleDestroyed;

            // Coordinates of the core and of ShipsRoot are the same (tower center at the origin).
            transform.localPosition = ship.Position.ToUnity();
            Vector3 toStation = ship.Station.ToUnity() - transform.localPosition;
            transform.localRotation = toStation.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toStation) : Quaternion.identity;
            _cityShotTimer = Random.Range(_cityShotInterval.x, _cityShotInterval.y);
            _hitFlashTimer = 0f;
            _view.Begin();
        }

        /// <summary>Unlinks the core ship before the GameObject goes back to the pool.</summary>
        public void Despawn()
        {
            if (Ship != null)
            {
                Ship.OnAimStarted -= HandleAimStarted;
                Ship.OnFire -= HandleFire;
                Ship.OnDestroyed -= HandleDestroyed;
                Ship = null;
            }
            _view.End();
        }

        private void Update()
        {
            if (Ship == null || !Ship.IsAlive)
            {
                return;
            }

            Vector3 previous = transform.localPosition;
            Ship.Tick(Time.deltaTime);
            if (Ship == null)
            {
                // Safety: the ship can be sent back to the pool by its own events (for example the shot that ends the match).
                return;
            }
            Vector3 current = Ship.Position.ToUnity();
            transform.localPosition = current;
            _hitFlashTimer -= Time.deltaTime;
            UpdateCityShots();

            // Nose first while flying; while aiming the nose points at the tower, so the warning laser comes out of it.
            // The ship eases into each new heading, so it never twitches.
            Vector3 look = Ship.Phase == ShipPhase.Aiming ? transform.parent.InverseTransformPoint(TargetWorld) - current : current - previous;
            if (look.sqrMagnitude > 0.000001f)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(look),
                    1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, _turnSeconds)));
            }
        }

        /// <summary>
        /// Weapon hit: the core decides whether the ship survives. A ship that survives shows the damage: its health
        /// bar shrinks and the hull blows up where the bolt landed.
        /// </summary>
        public void ReceiveHit(int damage, Vector3 point)
        {
            if (Ship == null)
            {
                return;
            }
            Ship.Hit(damage);
            // A fatal hit sends the ship back to the pool right away (Ship becomes null).
            if (Ship != null && Ship.IsAlive)
            {
                _view.ShowDamage((float)Ship.Health / Ship.MaxHealth);
                if (_hitFlashTimer <= 0f)
                {
                    _hitFlashTimer = _hitFlashInterval;
                    _spawner.HandleShipHit(Ship.Type, point);
                }
            }
        }

        // Between stations the ship fires down at a roof below it now and then.
        private void UpdateCityShots()
        {
            if (Ship.Phase != ShipPhase.Approaching)
            {
                return;
            }
            _cityShotTimer -= Time.deltaTime;
            if (_cityShotTimer > 0f)
            {
                return;
            }
            _cityShotTimer = Random.Range(_cityShotInterval.x, _cityShotInterval.y);
            if (_spawner.TryPickCityTarget(transform.position, out Vector3 target))
            {
                _view.ShowFire(target);
                _spawner.HandleCityShot(target);
            }
        }

        private void HandleAimStarted(EnemyShip ship)
        {
            _view.ShowAim(TargetWorld, ship.AimTime);
        }

        private void HandleFire(EnemyShip ship, TowerSide side, float damage)
        {
            // The core picks the next station right after this event, so the target is read here, before it changes.
            Vector3 impact = TargetWorld;
            _view.ShowFire(impact);
            _spawner.HandleShipFired(side, damage, impact);
        }

        private void HandleDestroyed(EnemyShip ship)
        {
            _spawner.HandleShipDestroyed(this);
        }
    }
}
