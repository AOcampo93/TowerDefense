using System;
using AlienInvaders.Game;
using TMPro;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Arcade-style initials entry for a new record, so no virtual keyboard is needed: the right thumbstick up and
    /// down changes the letter, right and left move between the three slots, and the trigger confirms.
    /// </summary>
    public class InitialsPicker : MonoBehaviour
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int Slots = 3;

        [Tooltip("Shown only while the player enters their initials.")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text[] _letters;
        [Tooltip("Underline that sits below the selected slot.")]
        [SerializeField] private RectTransform _cursor;
        [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _idleColor = Color.white;

        private readonly int[] _letterIndexes = new int[Slots];
        private int _slot;
        private bool _active;
        private int _startFrame;

        /// <summary>Raised with the three letters when the player pulls the trigger.</summary>
        public event Action<string> OnConfirmed;

        /// <summary>Text of the three slots right now.</summary>
        public string Initials
        {
            get
            {
                var letters = new char[Slots];
                for (int i = 0; i < Slots; i++)
                {
                    letters[i] = Alphabet[_letterIndexes[i]];
                }
                return new string(letters);
            }
        }

        private void Awake()
        {
            _root.SetActive(false);
        }

        /// <summary>Starts the entry with suggested letters (usually the last initials used on this headset).</summary>
        public void Begin(string suggestion)
        {
            for (int i = 0; i < Slots; i++)
            {
                char letter = suggestion != null && i < suggestion.Length ? char.ToUpperInvariant(suggestion[i]) : 'A';
                _letterIndexes[i] = Mathf.Max(0, Alphabet.IndexOf(letter));
            }
            _slot = 0;
            _active = true;
            // The trigger press that opened this screen must not confirm it at once.
            _startFrame = Time.frameCount;
            _root.SetActive(true);
            Refresh();
        }

        /// <summary>Removes the picker.</summary>
        public void Hide()
        {
            _active = false;
            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_active || Time.frameCount == _startFrame)
            {
                return;
            }

            bool changed = true;
            if (ControllerInput.RightStickFlicked(StickDirection.Up))
            {
                ChangeLetter(1);
            }
            else if (ControllerInput.RightStickFlicked(StickDirection.Down))
            {
                ChangeLetter(-1);
            }
            else if (ControllerInput.RightStickFlicked(StickDirection.Right))
            {
                _slot = Mathf.Min(Slots - 1, _slot + 1);
            }
            else if (ControllerInput.RightStickFlicked(StickDirection.Left))
            {
                _slot = Mathf.Max(0, _slot - 1);
            }
            else
            {
                changed = false;
            }

            if (changed)
            {
                Refresh();
            }

            if (ControllerInput.RightTriggerDown())
            {
                _active = false;
                OnConfirmed?.Invoke(Initials);
            }
        }

        // Steps through the alphabet and wraps around (Z goes back to A).
        private void ChangeLetter(int step)
        {
            _letterIndexes[_slot] = (_letterIndexes[_slot] + step + Alphabet.Length) % Alphabet.Length;
        }

        private void Refresh()
        {
            for (int i = 0; i < Slots; i++)
            {
                _letters[i].text = Alphabet[_letterIndexes[i]].ToString();
                _letters[i].color = i == _slot ? _selectedColor : _idleColor;
            }
            Vector2 position = _cursor.anchoredPosition;
            position.x = ((RectTransform)_letters[_slot].transform).anchoredPosition.x;
            _cursor.anchoredPosition = position;
        }
    }
}
