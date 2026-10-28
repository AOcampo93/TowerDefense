using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A small square screen next to the radar watch, along the left forearm, like a wrist computer. During the fight
    /// it offers two buttons, END NOW (the emergency elevator takes the player down to the results) and RESTART (down,
    /// and back to the start screen). They are pressed with the gun: a red dot shows where the gun points on the
    /// screen, and a button acts after a moment under fire (ConsoleButton). Its picture turns like the radar's, so the
    /// text reads upright however the wrist is turned.
    /// </summary>
    public class WristConsoleView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private WeaponController _weapon;
        [SerializeField] private Transform _head;
        [Tooltip("The console, shown only during the fight.")]
        [SerializeField] private GameObject _panel;
        [Tooltip("The part that turns so its text stays upright for the player.")]
        [SerializeField] private Transform _face;
        [SerializeField] private ConsoleButton _endButton;
        [SerializeField] private ConsoleButton _restartButton;
        [Tooltip("Red dot where the gun points on the screen.")]
        [SerializeField] private Transform _aimDot;
        [Tooltip("Layers of the console's colliders (UI).")]
        [SerializeField] private LayerMask _layers;

        private void Awake()
        {
            _panel.SetActive(false);
            _endButton.OnConfirmed += HandleEnd;
            _restartButton.OnConfirmed += HandleRestart;
        }

        private void OnDestroy()
        {
            _endButton.OnConfirmed -= HandleEnd;
            _restartButton.OnConfirmed -= HandleRestart;
        }

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
            _panel.SetActive(_game.InFight);
        }

        private void LateUpdate()
        {
            if (!_panel.activeSelf)
            {
                return;
            }

            // Like the radar: the screen lies on the forearm but its picture turns so "up" is up for the player.
            Vector3 intoArm = _panel.transform.forward;
            Vector3 top = Vector3.ProjectOnPlane(_head.up, intoArm);
            if (top.sqrMagnitude > 0.0001f)
            {
                _face.rotation = Quaternion.LookRotation(intoArm, top);
            }

            // Where the gun points: a dot on the screen, and the button under it lights up.
            ConsoleButton aimed = null;
            Transform muzzle = _weapon.Muzzle;
            bool onScreen = Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, Balance.PreciseTargetRange,
                _layers, QueryTriggerInteraction.Collide) && hit.collider.transform.IsChildOf(_panel.transform);
            if (onScreen)
            {
                hit.collider.TryGetComponent(out aimed);
                _aimDot.position = hit.point - intoArm * 0.002f;
            }
            _aimDot.gameObject.SetActive(onScreen);
            _endButton.Aimed = aimed == _endButton;
            _restartButton.Aimed = aimed == _restartButton;
        }

        private void HandleEnd()
        {
            _game.EndMatchNow(false);
        }

        private void HandleRestart()
        {
            _game.EndMatchNow(true);
        }
    }
}
