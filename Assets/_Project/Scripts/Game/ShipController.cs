using AlienInvaders.Core;
using AlienInvaders.Core.Ships;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Bridge between one core EnemyShip (the rules: life cycle, health, when to fire) and its GameObject. Every frame
    /// it advances the core ship and copies its position to the transform. Core events become visuals in the ShipView
    /// and damage in the ShipSpawner. Ships are pooled, so Init and Despawn take the place of Awake and Destroy.
    /// </summary>
    public class ShipController : MonoBehaviour, IShootable
    {
        [SerializeField] private ShipView _view;
        [Tooltip("Degrees per second the ship turns towards where it flies (or aims).")]
        [SerializeField] private float _turnSpeed = 240f;

        private ShipSpawner _spawner;

        /// <summary>The core ship shown by this GameObject (null while it waits in the pool).</summary>
        public EnemyShip Ship { get; private set; }

        /// <summary>World position of the facade point the ship is aiming at.</summary>
        public Vector3 TargetWorld => transform.parent.TransformPoint(Ship.TargetPoint.ToUnity());

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

            // Nose first while flying; while aiming the nose points at the tower, so the warning laser comes out of it.
            Vector3 look = Ship.Phase == ShipPhase.Aiming ? Ship.TargetPoint.ToUnity() - current : current - previous;
            if (look.sqrMagnitude > 0.000001f)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, Quaternion.LookRotation(look), _turnSpeed * Time.deltaTime);
            }
        }

        /// <summary>Weapon hit: the core decides whether the ship survives.</summary>
        public void ReceiveHit(int damage)
        {
            Ship?.Hit(damage);
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
