using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A button on a world-space panel that the controller pointer can hover and press. It needs a collider on the UI
    /// layer; the ControllerPointer finds it with a raycast, so no extra UI input module is needed.
    /// </summary>
    public class ARButton : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor = new Color(0.1f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color _hoverColor = new Color(0.1f, 0.8f, 1f, 1f);

        /// <summary>Raised when the player points at the button and pulls the trigger.</summary>
        public event Action OnPressed;

        private void OnEnable()
        {
            SetHovered(false);
        }

        /// <summary>Highlights the button while the pointer is on it.</summary>
        public void SetHovered(bool hovered)
        {
            _background.color = hovered ? _hoverColor : _normalColor;
        }

        /// <summary>Activates the button.</summary>
        public void Press()
        {
            OnPressed?.Invoke();
        }
    }
}
