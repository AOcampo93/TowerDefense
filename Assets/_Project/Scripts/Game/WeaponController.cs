using System;
using AlienInvaders.Core;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// The player's blaster in the right hand. It reads the trigger and asks the core WeaponHeat whether a shot is
    /// allowed (fire rate and overheating are rules, so they live in Core). Shots are instant beams (hitscan): a
    /// thin sphere cast works as a slight aim assist. Every shot is reported to the ScoreSystem, so misses lower
    /// the accuracy bonus.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [Tooltip("Tip of the barrel. Shots start here and travel along its forward axis.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Layers the shots can hit (Ships).")]
        [SerializeField] private LayerMask _targetLayers;

        /// <summary>True while the player can fire (combat states only).</summary>
        public bool Armed { get; private set; }

        /// <summary>Raised for every shot with the beam's start and end points and whether it hit a target.</summary>
        public event Action<Vector3, Vector3, bool> OnShot;

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            // The trigger calibrates the platform in AR, so it only fires once the fight is on.
            Armed = state == GameState.Defense || state == GameState.Respite || state == GameState.Critical;
        }

        private void Update()
        {
            if (!Armed)
            {
                return;
            }

            bool triggerHeld = ControllerInput.RightTriggerHeld();
            _game.Weapon.Tick(Time.deltaTime, triggerHeld);

            if (triggerHeld && _game.Weapon.TryFire())
            {
                Fire();
            }
        }

        private void Fire()
        {
            Vector3 origin = _muzzle.position;
            Vector3 direction = _muzzle.forward;
            bool hitSomething = Physics.SphereCast(origin, Balance.AimAssistRadius, direction, out RaycastHit hit,
                Balance.WeaponRange, _targetLayers, QueryTriggerInteraction.Collide);

            // Only things that can take damage count as hits. TryGetComponent runs only when the cast hits something.
            bool hitTarget = false;
            if (hitSomething && hit.collider.TryGetComponent(out IShootable target))
            {
                target.ReceiveHit(Balance.ShotDamage);
                hitTarget = true;
            }

            _game.Score.RegisterShot(hitTarget);

            Vector3 end = hitSomething ? origin + direction * hit.distance : origin + direction * Balance.WeaponRange;
            OnShot?.Invoke(origin, end, hitTarget);
        }
    }
}
