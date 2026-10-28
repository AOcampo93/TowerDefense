using System;
using System.Collections.Generic;
using AlienInvaders.Core;
using TMPro;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The feedback form on the scoreboard: how much fun the match was (1 to 5 stars), what we could improve (topics
    /// the player can pick, as many as they like) and a message. Pointing at the message box and pulling the trigger
    /// swaps the form for a keyboard on the same screen (BoardKeyboard) with the message above it; DONE brings the
    /// form back. The Quest's system keyboard does not open over this game, so it brings its own. "Send" hands the
    /// form to whoever listens (the Telemetry sends it to our database), thanks the player and closes the form a
    /// moment later. It starts empty every time it opens.
    /// </summary>
    public class FeedbackView : MonoBehaviour
    {
        [Tooltip("The five stars, left to right.")]
        [SerializeField] private ARButton[] _stars;
        [Tooltip("The topics to improve, and the name sent for each one.")]
        [SerializeField] private ARButton[] _topics;
        [SerializeField] private string[] _topicNames;
        [SerializeField] private ARButton _messageBox;
        [SerializeField] private TMP_Text _messageText;
        [Tooltip("Everything of the form, hidden while typing.")]
        [SerializeField] private GameObject _form;
        [Tooltip("The keyboard and the message above it, shown while typing.")]
        [SerializeField] private GameObject _typing;
        [SerializeField] private TMP_Text _typingText;
        [SerializeField] private BoardKeyboard _keyboard;
        [Tooltip("Characters of the message shown while typing (the end of a long message).")]
        [SerializeField] private int _typingShownChars = 190;
        [SerializeField] private ARButton _sendButton;
        [SerializeField] private ARButton _backButton;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private string _placeholder = "Point here and pull the trigger to type.";
        [SerializeField] private Color _placeholderColor = new Color(0.62f, 0.7f, 0.78f, 1f);
        [SerializeField] private Color _messageColor = new Color(0.93f, 0.95f, 0.97f, 1f);
        [SerializeField] private Color _thanksColor = new Color(0.98f, 0.73f, 0.2f, 1f);
        [SerializeField] private Color _hintColor = new Color(0.62f, 0.7f, 0.78f, 1f);
        [Tooltip("Seconds the thanks stays before the form closes by itself.")]
        [SerializeField] private float _thanksSeconds = 2.5f;

        private readonly List<string> _chosenTopics = new List<string>();
        private int _rating;
        private string _message = string.Empty;
        private float _closeTimer;
        private bool _sent;

        /// <summary>Raised each time the form opens.</summary>
        public event Action OnOpened;

        /// <summary>Raised when the player sends the form: stars (0 = none), chosen topics and message.</summary>
        public event Action<int, IReadOnlyList<string>, string> OnSubmitted;

        /// <summary>Raised when the form closes (back, or after the thanks).</summary>
        public event Action OnClosed;

        private void Awake()
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                int stars = i + 1;
                _stars[i].OnPressed += () => SetRating(stars);
            }
            for (int i = 0; i < _topics.Length; i++)
            {
                int topic = i;
                _topics[i].OnPressed += () => ToggleTopic(topic);
            }
            _messageBox.OnPressed += OpenKeyboard;
            _keyboard.OnKey += Type;
            _keyboard.OnDone += CloseKeyboard;
            _sendButton.OnPressed += Send;
            _backButton.OnPressed += Close;
        }

        private void OnEnable()
        {
            _rating = 0;
            _chosenTopics.Clear();
            _sent = false;
            _closeTimer = 0f;
            SetMessage(string.Empty);
            SetRating(0);
            foreach (ARButton topic in _topics)
            {
                topic.Selected = false;
            }
            _sendButton.gameObject.SetActive(true);
            CloseKeyboard();
            ShowStatus("Your feedback is anonymous. Thank you for helping us!", _hintColor);
            OnOpened?.Invoke();
        }

        private void Update()
        {
            if (_sent)
            {
                _closeTimer -= Time.deltaTime;
                if (_closeTimer <= 0f)
                {
                    Close();
                }
            }
        }

        /// <summary>Writes the message (the keyboard does this as the player types; tests and captures can too).</summary>
        public void SetMessage(string message)
        {
            _message = message ?? string.Empty;
            if (_message.Length > Balance.FeedbackMessageMaxChars)
            {
                _message = _message.Substring(0, Balance.FeedbackMessageMaxChars);
            }
            bool empty = _message.Trim().Length == 0;
            _messageText.text = empty ? _placeholder : _message;
            _messageText.color = empty ? _placeholderColor : _messageColor;
            // While typing, the end of the message and a caret.
            string tail = _message.Length > _typingShownChars ? "..." + _message.Substring(_message.Length - _typingShownChars) : _message;
            _typingText.text = tail + "<color=#40D1E6>|</color>";
        }

        /// <summary>Lights the stars up to this one (0 turns them all off).</summary>
        public void SetRating(int stars)
        {
            _rating = Mathf.Clamp(stars, 0, _stars.Length);
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].Selected = i < _rating;
            }
        }

        /// <summary>Picks a topic to improve, or drops it if it was picked.</summary>
        public void ToggleTopic(int index)
        {
            string topic = _topicNames[index];
            bool chosen = !_chosenTopics.Remove(topic);
            if (chosen)
            {
                _chosenTopics.Add(topic);
            }
            _topics[index].Selected = chosen;
        }

        private void OpenKeyboard()
        {
            _form.SetActive(false);
            _typing.SetActive(true);
        }

        private void CloseKeyboard()
        {
            _typing.SetActive(false);
            _form.SetActive(true);
        }

        // One key of the board keyboard: a character, or delete. A letter that starts a sentence is a capital.
        private void Type(string key)
        {
            if (key == BoardKeyboard.Delete)
            {
                SetMessage(_message.Length > 0 ? _message.Substring(0, _message.Length - 1) : _message);
                return;
            }
            string trimmed = _message.TrimEnd();
            bool sentenceStart = trimmed.Length == 0 || trimmed.EndsWith(".") || trimmed.EndsWith("!") || trimmed.EndsWith("?");
            SetMessage(_message + (sentenceStart ? key.ToUpperInvariant() : key.ToLowerInvariant()));
        }

        private void Send()
        {
            if (_sent)
            {
                return;
            }
            string message = _message.Trim();
            if (_rating == 0 && _chosenTopics.Count == 0 && message.Length == 0)
            {
                ShowStatus("Pick the stars, a topic or write a message first.", _hintColor);
                return;
            }
            OnSubmitted?.Invoke(_rating, _chosenTopics.ToArray(), message);
            _sent = true;
            _closeTimer = _thanksSeconds;
            _sendButton.gameObject.SetActive(false);
            ShowStatus("THANK YOU! We read every message.", _thanksColor);
        }

        private void Close()
        {
            _sent = false;
            OnClosed?.Invoke();
        }

        private void ShowStatus(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }
    }
}
