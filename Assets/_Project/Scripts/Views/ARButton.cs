using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A button on a world-space panel that the controller pointer can hover and press. It needs a collider on the UI
    /// layer; the ControllerPointer finds it with a raycast, so no extra UI input module is needed. It can also stay
    /// selected (a star of the rating, a topic of the feedback form): then it keeps a bright color, and its label can
    /// turn dark to stay readable on it.
    /// </summary>
    public class ARButton : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor = new Color(0.1f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color _hoverColor = new Color(0.1f, 0.8f, 1f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.98f, 0.73f, 0.2f, 1f);
        [Tooltip("Optional label recolored while the button is selected.")]
        [SerializeField] private Graphic _label;
        [SerializeField] private Color _selectedLabelColor = new Color(0.03f, 0.1f, 0.12f, 1f);

        private int _enabledFrame;
        private bool _hovered;
        private bool _selected;
        private Color _labelColor;
        private bool _labelColorKnown;

        /// <summary>Raised when the player points at the button and pulls the trigger.</summary>
        public event Action OnPressed;

        /// <summary>A selected button keeps its bright color until it is unselected.</summary>
        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            SetHovered(false);
            _enabledFrame = Time.frameCount;
        }

        /// <summary>Highlights the button while the pointer is on it.</summary>
        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Refresh();
        }

        /// <summary>Activates the button.</summary>
        public void Press()
        {
            // The trigger press that made the button appear (confirming initials) must not press it too.
            if (Time.frameCount != _enabledFrame)
            {
                OnPressed?.Invoke();
            }
        }

        private void Refresh()
        {
            _background.color = _hovered ? _hoverColor : _selected ? _selectedColor : _normalColor;
            if (_label != null)
            {
                // The label's own color is read before the first change (the form may select a button before the
                // button has run any code of its own).
                if (!_labelColorKnown)
                {
                    _labelColor = _label.color;
                    _labelColorKnown = true;
                }
                _label.color = _selected && !_hovered ? _selectedLabelColor : _labelColor;
            }
        }
    }
}
