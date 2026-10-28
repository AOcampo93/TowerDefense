using AlienInvaders.Core;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Laser pointer of the right controller for the AR panels: the "i" button of the start screen, and "Play again"
    /// and the feedback form at the end. It is only on in the Calibration and Results states, when the weapon is gone.
    /// A physics raycast against the UI layer finds ARButtons: point to highlight, pull the trigger to press.
    /// </summary>
    public class ControllerPointer : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private LineRenderer _ray;
        [Tooltip("Layers with button colliders (UI).")]
        [SerializeField] private LayerMask _uiLayers;
        [SerializeField] private float _maxDistance = 4f;

        private ARButton _hovered;

        /// <summary>True while the laser points at a button (the trigger then presses it and does nothing else).</summary>
        public bool IsOverButton => _hovered != null;

        private void Update()
        {
            if (_game.State != GameState.Results && _game.State != GameState.Calibration)
            {
                if (_ray.enabled)
                {
                    _ray.enabled = false;
                    Hover(null);
                }
                return;
            }

            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            float length = _maxDistance;
            ARButton button = null;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, _maxDistance, _uiLayers, QueryTriggerInteraction.Collide))
            {
                length = hit.distance;
                hit.collider.TryGetComponent(out button);
            }
            Hover(button);

            _ray.enabled = true;
            _ray.SetPosition(0, origin);
            _ray.SetPosition(1, origin + direction * length);

            if (_hovered != null && ControllerInput.RightTriggerDown())
            {
                // A short tick in the hand, like a key giving way.
                ControllerHaptics.PulseRight(0.3f, 0.03f);
                _hovered.Press();
            }
        }

        private void Hover(ARButton button)
        {
            if (button == _hovered)
            {
                return;
            }
            if (_hovered != null)
            {
                _hovered.SetHovered(false);
            }
            _hovered = button;
            if (_hovered != null)
            {
                _hovered.SetHovered(true);
            }
        }
    }
}
