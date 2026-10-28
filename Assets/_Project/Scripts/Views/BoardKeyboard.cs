using System;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A keyboard drawn on the scoreboard and typed with the controller's laser, one key per trigger pull. The Quest's
    /// own system keyboard does not open over this game, so the feedback form brings its own. Each key sends its text;
    /// three special keys send a space, delete the last letter or close the keyboard.
    /// </summary>
    public class BoardKeyboard : MonoBehaviour
    {
        /// <summary>Key value that deletes the last character.</summary>
        public const string Delete = "<del>";

        /// <summary>Key value that closes the keyboard.</summary>
        public const string Done = "<done>";

        [Tooltip("Every key, and what each one types (a letter, a space, or the Delete and Done values).")]
        [SerializeField] private ARButton[] _keys;
        [SerializeField] private string[] _values;

        /// <summary>Raised with the text of a key (or Delete), whenever a key is pressed.</summary>
        public event Action<string> OnKey;

        /// <summary>Raised when the player presses DONE.</summary>
        public event Action OnDone;

        private void Awake()
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                string value = _values[i];
                _keys[i].OnPressed += () => Press(value);
            }
        }

        private void Press(string value)
        {
            if (value == Done)
            {
                OnDone?.Invoke();
            }
            else
            {
                OnKey?.Invoke(value);
            }
        }
    }
}
