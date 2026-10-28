using System;
using AlienInvaders.Core;
using AlienInvaders.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A button of the wrist console, pressed with the gun: it is shot like a ship (IShootable) and acts only after
    /// it has been under fire for a moment (core HoldToConfirm), so a stray bolt never ends the match. A bar fills
    /// while the bolts keep coming and the left hand buzzes with every hit. It lights up while the gun aims at it.
    /// </summary>
    public class ConsoleButton : MonoBehaviour, IShootable
    {
        [SerializeField] private Image _background;
        [Tooltip("Bar that fills from the left while the button is under fire.")]
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private Color _normalColor = new Color(0.7f, 0.2f, 0.1f, 1f);
        [SerializeField] private Color _aimedColor = new Color(1f, 0.4f, 0.2f, 1f);
        [SerializeField] private string _idleHint = "AIM AND HOLD FIRE";
        [SerializeField] private string _holdingHint = "KEEP FIRING...";

        private readonly HoldToConfirm _hold = new HoldToConfirm(Balance.ConsoleHoldSeconds, Balance.ConsoleHitGraceSeconds);
        private bool _aimed;

        /// <summary>Raised once the button has been held under fire long enough.</summary>
        public event Action OnConfirmed;

        /// <summary>Lit while the gun points at it.</summary>
        public bool Aimed
        {
            set
            {
                _aimed = value;
                _background.color = _aimed ? _aimedColor : _normalColor;
            }
        }

        private void OnEnable()
        {
            _hold.Reset();
            Aimed = false;
            Refresh();
        }

        /// <summary>A bolt hit the button.</summary>
        public void ReceiveHit(int damage, Vector3 point)
        {
            _hold.Hit();
            ControllerHaptics.PulseLeft(0.35f, 0.04f);
        }

        private void Update()
        {
            if (_hold.Tick(Time.deltaTime))
            {
                OnConfirmed?.Invoke();
            }
            Refresh();
        }

        private void Refresh()
        {
            _fill.localScale = new Vector3(_hold.Progress, 1f, 1f);
            _hint.text = _hold.Progress > 0f ? _holdingHint : _idleHint;
        }
    }
}
