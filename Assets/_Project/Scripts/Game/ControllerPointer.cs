using AlienInvaders.Core;
using AlienInvaders.Views;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Laser pointer of the right controller for the AR panels ("Play again"). It is only on in the Results state,
    /// when the weapon is gone. A physics raycast against the UI layer finds ARButtons: point to highlight, pull the
    /// trigger to press.
    /// </summary>
    public class ControllerPointer : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private LineRenderer _ray;
        [Tooltip("Layers with button colliders (UI).")]
        [SerializeField] private LayerMask _uiLayers;
        [SerializeField] private float _maxDistance = 4f;

        private ARButton _hovered;

        private void Update()
        {
            if (_game.State != GameState.Results)
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
